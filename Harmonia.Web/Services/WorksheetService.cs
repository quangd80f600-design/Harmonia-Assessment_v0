using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Harmonia.Web.Data;
using Harmonia.Web.Models;
using Harmonia.Web.Models.Entities;
using Harmonia.Web.Services.Security;
using Harmonia.Web.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Harmonia.Web.Services;

public sealed class WorksheetOptions { public bool Enabled { get; set; } }

public sealed partial class WorksheetService(IDbContextFactory<HarmoniaDbContext> factory, BusinessAccess access,
    CurrentActor actor, IOptions<WorksheetOptions> options, IOptions<InterventionManagementOptions> interventions)
{
    public bool Enabled => options.Value.Enabled;
    public bool CanWrite => Enabled && interventions.Value.Enabled && actor.Id is not null;
    internal static string Fingerprint(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    internal static bool ValidPicks(IReadOnlyList<WorksheetPick> picks) => picks.Count <= 200 && picks.All(x => x.Id > 0)
        && picks.Select(x => x.Id).Distinct().Count() == picks.Count
        && picks.Where(x => x.Selected).All(x => x.Order is > 0 and <= short.MaxValue)
        && picks.Where(x => x.Selected).Select(x => x.Order).Distinct().Count() == picks.Count(x => x.Selected);

    public async Task<TheoryPage?> GetTheoriesAsync(long? id, TheoryInput? input = null, CancellationToken ct = default)
    {
        if (!Enabled || actor.Id is null) return null;
        await using var db = await factory.CreateDbContextAsync(ct);
        var rows = await db.InterventionTheories.AsNoTracking().OrderBy(t => t.SubskillId).ThenBy(t => t.Title)
            .Select(t => new TheoryChoice(t.InterventionTheoryId,t.SubskillId,t.Title,t.Content,t.Status)).ToListAsync(ct);
        var found = id.HasValue ? rows.SingleOrDefault(x => x.Id == id) : null;
        if (id.HasValue && found is null) return null;
        var skills = await db.Subskills.AsNoTracking().Where(s => s.Status == "Active" && s.KnowledgeArea.Status == "Active")
            .OrderBy(s => s.SubskillCode).Select(s => new TheorySkill(s.SubskillId,s.SubskillCode,s.SubskillName) {
                Grade=s.KnowledgeArea.CurriculumUnit.Grade, UnitId=s.KnowledgeArea.CurriculumUnitId,
                UnitName=s.KnowledgeArea.CurriculumUnit.UnitName, UnitNumber=s.KnowledgeArea.CurriculumUnit.UnitNumber,
                AreaId=s.KnowledgeAreaId, AreaName=s.KnowledgeArea.KnowledgeAreaName }).ToListAsync(ct);
        input ??= found is null ? new() : new() { SubskillId=found.SubskillId,Title=found.Title,Content=found.Content,Status=found.Status,Version=Fingerprint(found) };
        return new(input,id,CanWrite && (id is null || actor.IsManager),rows,skills);
    }
    public async Task<WorksheetWrite> SaveTheoryAsync(long? id, TheoryInput input, CancellationToken ct = default)
    {
        if (!CanWrite || id.HasValue && !actor.IsManager) return new(false,"Bạn không có quyền sửa lý thuyết dùng chung.");
        input.Title=input.Title?.Trim()??"";input.Content=input.Content?.Trim()??"";
        if (InterventionManagementService.Validate(input) is string error) return new(false,error);
        if (input.Status is not ("Active" or "Inactive")) return new(false,"Trạng thái lý thuyết không hợp lệ.");
        await using var db=await factory.CreateDbContextAsync(ct);
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        if (!await db.Subskills.AnyAsync(s=>s.SubskillId==input.SubskillId && s.Status=="Active" && s.KnowledgeArea.Status=="Active",ct))
            return new(false,"Chọn nội dung kiến thức đang hoạt động.");
        InterventionTheory row;
        if (id is long key)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM public.\"InterventionTheory\" WHERE \"InterventionTheory_ID\"={key} FOR UPDATE",ct);
            var existing=await db.InterventionTheories.SingleOrDefaultAsync(t=>t.InterventionTheoryId==key,ct);
            if (existing is null) return new(false,"Không tìm thấy lý thuyết.");
            if (input.Version!=Fingerprint(new TheoryChoice(key,existing.SubskillId,existing.Title,existing.Content,existing.Status))) return new(false,"Lý thuyết đã thay đổi. Hãy tải lại trước khi sửa.");
            if(existing.SubskillId!=input.SubskillId) return new(false,"Không đổi Subskill của lý thuyết đã tạo; hãy tạo lý thuyết mới.");
            row=existing;
        }
        else { row=new();db.InterventionTheories.Add(row); }
        row.SubskillId=input.SubskillId;row.Title=input.Title;row.Content=input.Content;row.Status=input.Status;
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return new(true,"Đã lưu lý thuyết. Các phiếu đã lưu giữ nguyên nội dung.",row.InterventionTheoryId);
    }

    public async Task<WorksheetComposer?> GetComposerAsync(long assessmentId,long studentId,long? packageId=null,WorksheetInput? input=null,CancellationToken ct=default)
    {
        if(!CanWrite) return null;
        await using var db=await factory.CreateDbContextAsync(ct);
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,ct);
        await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY",ct);
        return await Composer(db,assessmentId,studentId,packageId,input,ct);
    }
    private async Task<WorksheetComposer?> Composer(HarmoniaDbContext db,long assessmentId,long studentId,long? packageId,WorksheetInput? input,CancellationToken ct)
    {
        var a=await db.Assessments.AsNoTracking().Where(access.AssessmentWriteScope()).SingleOrDefaultAsync(a=>a.AssessmentId==assessmentId && (a.Status=="Open" || a.Status=="Completed"),ct);
        if(a is null) return null;
        var s=await db.Students.AsNoTracking().Where(s=>s.StudentId==studentId && s.ClassMemberships.Any(m=>m.ClassId==a.ClassId && m.StartDate<=a.AssessmentDate && (m.EndDate==null || m.EndDate>a.AssessmentDate)))
            .Select(s=>new{s.FullName}).SingleOrDefaultAsync(ct);
        if(s is null) return null;
        var attemptId=await db.Attempts.Where(at=>at.AssessmentId==assessmentId && at.StudentId==studentId).Select(at=>(long?)at.AttemptId).SingleOrDefaultAsync(ct);
        var evidence=await IndividualDiagnosisService.BuildEvidenceQuery(db.QuestionInTests.AsNoTracking(),
            db.Responses.AsNoTracking().Where(r=>attemptId.HasValue),a.TestId,attemptId??0).ToListAsync(ct);
        var skills=evidence.GroupBy(q=>new{q.SubskillId,q.SubskillCode,q.SubskillName}).Where(g=>g.Any(q=>q.IsCorrect==false))
            .OrderBy(g=>g.Key.SubskillCode).Select(g=>new WorksheetSkill(g.Key.SubskillId,g.Key.SubskillCode,g.Key.SubskillName,g.Count(),g.Count(q=>q.IsCorrect.HasValue),g.Count(q=>q.IsCorrect==false))).ToArray();
        var ids=skills.Select(s=>s.Id).ToArray();
        var theories=await db.InterventionTheories.AsNoTracking().Where(t=>t.Status=="Active" && ids.Contains(t.SubskillId))
            .OrderBy(t=>t.InterventionTheoryId).Select(t=>new TheoryChoice(t.InterventionTheoryId,t.SubskillId,t.Title,t.Content,t.Status)).ToListAsync(ct);
        var questions=await db.Questions.AsNoTracking().Where(q=>q.Status=="Active" && q.UsagePurpose==QuestionPurposes.ForIntervention && ids.Contains(q.SubskillId))
            .OrderBy(q=>q.QuestionCode).Select(q=>new WorksheetQuestion(q.QuestionId,q.SubskillId,q.QuestionCode,q.QuestionText,q.OptionA,q.OptionB,q.OptionC,q.OptionD,q.AnswerKey,q.Explanation)).ToListAsync(ct);
        InterventionPackage? package=null;List<InterventionPackageTheory> savedT=[];List<InterventionPackageQuestion> savedQ=[];
        if(packageId is long p)
        {
            package=await db.InterventionPackages.AsNoTracking().SingleOrDefaultAsync(x=>x.InterventionPackageId==p && x.AssessmentId==assessmentId && x.Status=="Draft" && !x.InterventionPackageStudents.Any() && !x.InterventionPackageResources.Any(),ct);
            if(package is null) return null;
            savedT=await db.InterventionPackageTheories.AsNoTracking().Where(x=>x.InterventionPackageId==p).OrderBy(x=>x.SortOrder).ToListAsync(ct);
            savedQ=await db.InterventionPackageQuestions.AsNoTracking().Where(x=>x.InterventionPackageId==p).OrderBy(x=>x.QuestionNumber).ToListAsync(ct);
            if(savedT.Count+savedQ.Count==0) return null;
        }
        var version=Fingerprint(new{assessmentId,studentId,packageId,a.Status,a.AssessmentDate,evidence,theories,questions,package,savedT,savedQ});
        input??=new(){PackageName=package?.PackageName??("Phiếu ôn tập — "+s.FullName),Instructions=package?.Instructions,Version=version,
            Theories=theories.Select((t,i)=>new WorksheetPick{Id=t.Id,Order=i+1,Selected=savedT.Any(x=>x.InterventionTheoryId==t.Id)}).ToList(),
            Questions=questions.Select((q,i)=>new WorksheetPick{Id=q.Id,Order=savedQ.FirstOrDefault(x=>x.QuestionId==q.Id)?.QuestionNumber??i+1,Selected=savedQ.Any(x=>x.QuestionId==q.Id)}).ToList()};
        return new(assessmentId,studentId,s.FullName,a.AssessmentName,packageId,input,skills,theories,questions)
            { HasAttempt=attemptId.HasValue,QuestionCount=evidence.Count,ResponseCount=evidence.Count(q=>q.IsCorrect.HasValue) };
    }
    public async Task<WorksheetWrite> SaveAsync(long assessmentId,long studentId,long? packageId,WorksheetInput input,CancellationToken ct=default)
    {
        if(!CanWrite) return new(false,"Chưa được phép soạn phiếu.");
        input.Theories ??= []; input.Questions ??= [];
        input.PackageName=input.PackageName?.Trim()??"";
        if(InterventionManagementService.Validate(input) is string err) return new(false,err);
        if(!ValidPicks(input.Theories)||!ValidPicks(input.Questions)) return new(false,"Danh sách hoặc thứ tự nội dung không hợp lệ; mỗi loại tối đa 200 mục.");
        if(!input.Questions.Any(x=>x.Selected)) return new(false,"Chọn ít nhất một câu luyện tập.");
        await using var db=await factory.CreateDbContextAsync(ct);
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        // Serializable prevents a changed source/membership being silently used; no automatic retry after commit ambiguity.
        if(packageId is long p) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM public.\"InterventionPackage\" WHERE \"InterventionPackage_ID\"={p} FOR UPDATE",ct);
        var page=await Composer(db,assessmentId,studentId,packageId,null,ct);
        if(page is null) return new(false,"Không tìm thấy dữ liệu hợp lệ hoặc bạn không có quyền sửa.");
        if(input.Version!=page.Input.Version) return new(false,"Kết quả, nội dung hoặc phiếu đã thay đổi. Hãy tải lại và kiểm tra trước khi lưu.");
        var ts=input.Theories.Where(x=>x.Selected).OrderBy(x=>x.Order).ToArray();var qs=input.Questions.Where(x=>x.Selected).OrderBy(x=>x.Order).ToArray();
        if(ts.Any(x=>!page.Theories.Any(t=>t.Id==x.Id)) || qs.Any(x=>!page.Questions.Any(q=>q.Id==x.Id))) return new(false,"Chỉ chọn lý thuyết và câu luyện thuộc đúng Subskill được đề xuất.");
        var id=await PersistAsync(db,assessmentId,packageId,input,page,ct);
        await tx.CommitAsync(ct);return new(true,"Đã lưu phiếu nháp. Hãy kiểm tra rồi giao gói cho học sinh.",id);
    }
    private static async Task<long> PersistAsync(HarmoniaDbContext db,long assessmentId,long? packageId,WorksheetInput input,WorksheetComposer page,CancellationToken ct)
    {
        var ts=input.Theories.Where(x=>x.Selected).OrderBy(x=>x.Order).ToArray();var qs=input.Questions.Where(x=>x.Selected).OrderBy(x=>x.Order).ToArray();
        // Keep each Subskill in one section; preserve first occurrence of each section and order inside it.
        qs=qs.GroupBy(x=>page.Questions.Single(q=>q.Id==x.Id).SubskillId).SelectMany(g=>g).ToArray();
        var package=packageId is long key?await db.InterventionPackages.SingleAsync(p=>p.InterventionPackageId==key,ct):new InterventionPackage{AssessmentId=assessmentId,Status="Draft"};
        package.PackageName=input.PackageName;package.Instructions=string.IsNullOrWhiteSpace(input.Instructions)?null:input.Instructions.Trim();
        if(packageId is null){db.InterventionPackages.Add(package);await db.SaveChangesAsync(ct);}
        else
        {
            await db.InterventionPackageTheories.Where(t=>t.InterventionPackageId==packageId).ExecuteDeleteAsync(ct);
            await db.InterventionPackageQuestions.Where(q=>q.InterventionPackageId==packageId).ExecuteDeleteAsync(ct);
        }
        for(var i=0;i<ts.Length;i++){var t=page.Theories.Single(t=>t.Id==ts[i].Id);db.InterventionPackageTheories.Add(new(){InterventionPackageId=package.InterventionPackageId,InterventionTheoryId=t.Id,SortOrder=(short)(i+1),SnStitle=t.Title,SnScontent=t.Content,SnSsubskillId=t.SubskillId});}
        for(var i=0;i<qs.Length;i++){var q=page.Questions.Single(q=>q.Id==qs[i].Id);db.InterventionPackageQuestions.Add(new(){InterventionPackageId=package.InterventionPackageId,QuestionId=q.Id,QuestionNumber=(short)(i+1),SnSquestionText=q.Text,SnSoptionA=q.A,SnSoptionB=q.B,SnSoptionC=q.C,SnSoptionD=q.D,SnSanswerKey=q.Key,SnSexplanation=q.Explanation,SnSsubskillId=q.SubskillId});}
        await db.SaveChangesAsync(ct);return package.InterventionPackageId;
    }

    public async Task<WorksheetPrint?> GetPrintAsync(long id,long? studentId,CancellationToken ct=default)
    {
        if(!Enabled||actor.Id is null) return null;
        await using var db=await factory.CreateDbContextAsync(ct);
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,ct);
        await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY",ct);
        var p=await db.InterventionPackages.AsNoTracking().Where(p=>p.InterventionPackageId==id && db.Assessments.Where(access.AssessmentScope()).Any(a=>a.AssessmentId==p.AssessmentId))
            .Select(p=>new{p.PackageName,p.Instructions,p.Status,p.Assessment.AssessmentName,p.Assessment.Class.ClassName}).SingleOrDefaultAsync(ct);
        if(p is null) return null;
        string? name=null;
        if(studentId is long s){name=await db.InterventionPackageStudents.Where(x=>x.InterventionPackageId==id && x.StudentId==s).Select(x=>x.Student.FullName).SingleOrDefaultAsync(ct);if(name is null)return null;}
        var t=await db.InterventionPackageTheories.AsNoTracking().Where(x=>x.InterventionPackageId==id).OrderBy(x=>x.SortOrder).Select(x=>new WorksheetTheory(x.SortOrder,x.SnSsubskillId,x.SnStitle,x.SnScontent)).ToListAsync(ct);
        // Do not load answer key/explanation, even transiently, for the student print route.
        var q=await db.InterventionPackageQuestions.AsNoTracking().Where(x=>x.InterventionPackageId==id).OrderBy(x=>x.QuestionNumber).Select(x=>new WorksheetPublicQuestion(x.QuestionNumber,x.SnSsubskillId,x.SnSquestionText,x.SnSoptionA,x.SnSoptionB,x.SnSoptionC,x.SnSoptionD)).ToListAsync(ct);
        if(q.Count==0) return null;
        var ids=t.Select(x=>x.SubskillId).Concat(q.Select(x=>x.SubskillId)).Distinct().ToArray();
        var skills=await db.Subskills.AsNoTracking().Where(x=>ids.Contains(x.SubskillId)).OrderBy(x=>x.SubskillCode).Select(x=>new TheorySkill(x.SubskillId,x.SubskillCode,x.SubskillName)).ToListAsync(ct);
        var order=q.Select(x=>x.SubskillId).Concat(t.Select(x=>x.SubskillId)).Distinct().ToArray();
        return new(id,p.PackageName,p.Instructions,p.ClassName,p.AssessmentName,name,p.Status,order.Select(s=>skills.Single(x=>x.Id==s)).ToArray(),t,q);
    }
    public async Task<long?> GetEditAssessmentAsync(long id,CancellationToken ct=default)
    {
        if(!CanWrite) return null;
        await using var db=await factory.CreateDbContextAsync(ct);
        return await db.InterventionPackages.AsNoTracking().Where(p=>p.InterventionPackageId==id && p.Status=="Draft" && !p.InterventionPackageStudents.Any()
            && db.Assessments.Where(access.AssessmentWriteScope()).Any(a=>a.AssessmentId==p.AssessmentId))
            .Select(p=>(long?)p.AssessmentId).SingleOrDefaultAsync(ct);
    }
    public async Task<WorksheetAnswerPage?> GetAnswersAsync(long id,CancellationToken ct=default)
    {
        if(!Enabled||actor.Id is null) return null;
        await using var db=await factory.CreateDbContextAsync(ct);
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,ct);
        await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY",ct);
        var name=await db.InterventionPackages.AsNoTracking().Where(p=>p.InterventionPackageId==id && db.Assessments.Where(access.AssessmentScope()).Any(a=>a.AssessmentId==p.AssessmentId)).Select(p=>p.PackageName).SingleOrDefaultAsync(ct);
        if(name is null) return null;
        var rows=await db.InterventionPackageQuestions.AsNoTracking().Where(x=>x.InterventionPackageId==id).OrderBy(x=>x.QuestionNumber).Select(x=>new WorksheetAnswer(x.QuestionNumber,x.SnSanswerKey,x.SnSexplanation)).ToListAsync(ct);
        return rows.Count==0?null:new(id,name,rows);
    }
}
