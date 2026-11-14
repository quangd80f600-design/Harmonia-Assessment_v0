using System.Data;
using Harmonia.Web.Data;
using Harmonia.Web.Models;
using Harmonia.Web.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Harmonia.Web.Services;

public sealed partial class WorksheetService
{
    private sealed record GroupEvidence(long StudentId,long AttemptId,long QuestionId,long SubskillId,bool Correct);
    public async Task<WorksheetGroupPage?> GetGroupAsync(long assessmentId,long subskillId,WorksheetGroupInput? input=null,CancellationToken ct=default)
    {
        if(!CanWrite||assessmentId<=0||subskillId<=0)return null;
        await using var db=await factory.CreateDbContextAsync(ct);
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,ct);
        await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY",ct);
        return await GroupCore(db,assessmentId,subskillId,input,ct);
    }
    public async Task<WorksheetExactGroupsPage?> GetExactGroupsAsync(long assessmentId,CancellationToken ct=default)
    {
        if(!CanWrite || assessmentId<=0)return null;
        await using var db=await factory.CreateDbContextAsync(ct);
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,ct);
        await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY",ct);
        var a=await db.Assessments.AsNoTracking().Where(access.AssessmentWriteScope())
            .Where(a=>a.AssessmentId==assessmentId && (a.Status=="Open" || a.Status=="Completed"))
            .Select(a=>new {a.TestId}).SingleOrDefaultAsync(ct);
        if(a is null)return null;
        var ids=await db.QuestionInTests.AsNoTracking().Where(q=>q.TestId==a.TestId)
            .Select(q=>q.SnSsubskillId).Distinct().OrderBy(id=>id).ToArrayAsync(ct);
        if(ids.Length==0 || ids.Length>200)return null;
        var page=await GroupCore(db,assessmentId,ids[0],new(){SubskillIds=ids,MatchMode="Any"},ct);
        if(page is null)return null;
        var exact=WorksheetExactGrouping.Partition(page.Students,page.AvailableSkills);
        return new(assessmentId,page.Content.AssessmentName,page.ClassName,page.RosterCount,
            page.RosterCount-page.MissingAttemptCount-page.IncompleteResponseCount-exact.Sum(g=>g.Students.Count),
            page.MissingAttemptCount,page.IncompleteResponseCount,exact);
    }
    private async Task<WorksheetGroupPage?> GroupCore(HarmoniaDbContext db,long assessmentId,long subskillId,WorksheetGroupInput? input,CancellationToken ct)
    {
        var a=await db.Assessments.AsNoTracking().Include(a=>a.Class).Where(access.AssessmentWriteScope())
            .SingleOrDefaultAsync(a=>a.AssessmentId==assessmentId&&(a.Status=="Open"||a.Status=="Completed"),ct);
        if(a is null)return null;
        var selectedIds=input?.SubskillIds is {Length:>0} picks ? picks : [subskillId];
        var match=input?.MatchMode??"Any";
        if(selectedIds.Length>200||selectedIds.Any(id=>id<=0)||selectedIds.Distinct().Count()!=selectedIds.Length||match is not ("Any" or "All" or "Exact"))return null;
        selectedIds=selectedIds.Order().ToArray();
        var allSnapshots=await db.QuestionInTests.AsNoTracking().Where(q=>q.TestId==a.TestId)
            .OrderBy(q=>q.QuestionNumber).Select(q=>new{q.QuestionInTestId,q.SnSsubskillId,q.SnSsubskill.SubskillCode,q.SnSsubskill.SubskillName}).ToListAsync(ct);
        if(selectedIds.Any(id=>!allSnapshots.Any(q=>q.SnSsubskillId==id)))return null;
        var snapshots=allSnapshots.Where(q=>selectedIds.Contains(q.SnSsubskillId)).ToArray();
        if(snapshots.Length==0)return null;
        var roster=await db.Students.AsNoTracking().Where(s=>s.ClassMemberships.Any(m=>m.ClassId==a.ClassId&&m.StartDate<=a.AssessmentDate&&(m.EndDate==null||m.EndDate>a.AssessmentDate)))
            .OrderBy(s=>s.StudentCode).Select(s=>new{s.StudentId,s.StudentCode,s.FullName}).ToListAsync(ct);
        var ids=roster.Select(s=>s.StudentId).ToArray();var basisSnapshots=match=="Exact"?allSnapshots.ToArray():snapshots;
        var questionIds=basisSnapshots.Select(q=>q.QuestionInTestId).ToArray();
        var attempts=await db.Attempts.AsNoTracking().Where(t=>t.AssessmentId==assessmentId&&ids.Contains(t.StudentId))
            .OrderBy(t=>t.StudentId).Select(t=>new{t.StudentId,t.AttemptId}).ToListAsync(ct);
        var evidence=await db.Responses.AsNoTracking().Where(r=>r.Attempt.AssessmentId==assessmentId&&ids.Contains(r.Attempt.StudentId)&&questionIds.Contains(r.QuestionInTestId))
            .OrderBy(r=>r.Attempt.StudentId).ThenBy(r=>r.QuestionInTestId)
            .Select(r=>new GroupEvidence(r.Attempt.StudentId,r.AttemptId,r.QuestionInTestId,r.QuestionInTest.SnSsubskillId,r.IsCorrect)).ToListAsync(ct);
        var grouped=evidence.GroupBy(r=>r.StudentId).ToDictionary(g=>g.Key,g=>g.ToArray());
        var candidates=roster.Where(s=>grouped.ContainsKey(s.StudentId))
            .Select(s=>new WorksheetGroupStudent(s.StudentId,s.StudentCode,s.FullName,grouped[s.StudentId].Count(r=>r.Correct),grouped[s.StudentId].Length,basisSnapshots.Length)
            {Cells=basisSnapshots.GroupBy(q=>q.SnSsubskillId).Select(g=>new WorksheetGroupCell(g.Key,grouped[s.StudentId].Count(r=>r.SubskillId==g.Key&&r.Correct),grouped[s.StudentId].Count(r=>r.SubskillId==g.Key),g.Count())).ToArray()});
        var students=candidates.Where(s=>match=="Exact"?WorksheetExactGrouping.Matches(s,selectedIds)
            :match=="All"?selectedIds.All(id=>s.Cells.Any(c=>c.SubskillId==id&&c.ResponseCount>c.CorrectCount))
            :s.Cells.Any(c=>c.ResponseCount>c.CorrectCount)).ToArray();
        var theories=await db.InterventionTheories.AsNoTracking().Where(t=>t.Status=="Active"&&selectedIds.Contains(t.SubskillId))
            .OrderBy(t=>t.InterventionTheoryId).Select(t=>new TheoryChoice(t.InterventionTheoryId,t.SubskillId,t.Title,t.Content,t.Status)).ToArrayAsync(ct);
        var questions=await db.Questions.AsNoTracking().Where(q=>q.Status=="Active"&&q.UsagePurpose==QuestionPurposes.ForIntervention&&selectedIds.Contains(q.SubskillId))
            .OrderBy(q=>q.QuestionCode).Select(q=>new WorksheetQuestion(q.QuestionId,q.SubskillId,q.QuestionCode,q.QuestionText,q.OptionA,q.OptionB,q.OptionC,q.OptionD,q.AnswerKey,q.Explanation)).ToArrayAsync(ct);
        var version=Fingerprint(new{assessmentId,selectedIds,match,a.ClassId,a.AssessmentDate,a.Status,snapshots,basisSnapshots,roster,attempts,evidence,theories,questions});
        var defaultName="Phiếu ôn tập — "+string.Join(", ",snapshots.GroupBy(q=>q.SnSsubskillId).Select(g=>g.First().SubskillName));
        if(defaultName.Length>200)defaultName="Phiếu ôn tập — "+snapshots[0].SubskillCode;
        var initial=input is null||string.IsNullOrEmpty(input.Version);
        if(initial) input=new(){PackageName=string.IsNullOrWhiteSpace(input?.PackageName)?defaultName:input.PackageName,Version=version,SubskillIds=selectedIds,MatchMode=match,
            Theories=theories.Select((t,i)=>new WorksheetPick{Id=t.Id,Order=i+1}).ToList(),
            Questions=questions.Select((q,i)=>new WorksheetPick{Id=q.Id,Order=i+1}).ToList()};
        if(initial && match=="Exact")input!.StudentIds=students.Take(200).Select(s=>s.StudentId).ToArray();
        input!.StudentIds??=[];input.Theories??=[];input.Questions??=[];
        var content=new WorksheetComposer(assessmentId,0,"Nhóm học sinh",a.AssessmentName,null,input,
            snapshots.GroupBy(q=>q.SnSsubskillId).Select(g=>new WorksheetSkill(g.Key,g.First().SubskillCode,g.First().SubskillName,g.Count(),evidence.Count(r=>r.SubskillId==g.Key),evidence.Count(r=>r.SubskillId==g.Key&&!r.Correct))).ToArray(),theories,questions)
            {HasAttempt=attempts.Count>0,QuestionCount=snapshots.Length,ResponseCount=evidence.Count};
        // Keep S2's aggregation intact. Recipient eligibility is a separate, temporal-roster decision.
        var summary=AssessmentAnalysisService.Summarize(await AssessmentAnalysisService.BuildQuestionQuery(db.Assessments.AsNoTracking(),assessmentId,null).ToListAsync(ct));
        return new(content,students,roster.Count,roster.Count-attempts.Count,
            attempts.Count(t=>!grouped.TryGetValue(t.StudentId,out var r)||r.Length<basisSnapshots.Length),AnalysisPercentage.Calculate(summary.Where(r=>selectedIds.Contains(r.SubskillId)).Sum(r=>r.IncorrectResponses),summary.Where(r=>selectedIds.Contains(r.SubskillId)).Sum(r=>r.TotalResponses))) { ClassName=a.Class.ClassName,AvailableSkills=allSnapshots.GroupBy(q=>q.SnSsubskillId).Select(g=>new TheorySkill(g.Key,g.First().SubskillCode,g.First().SubskillName)).ToArray() };
    }
    private static async Task<string?> DraftVersion(HarmoniaDbContext db,long packageId,long assessmentId,IReadOnlyList<long> subskillIds,CancellationToken ct)
    {
        var p=await db.InterventionPackages.AsNoTracking().Where(p=>p.InterventionPackageId==packageId&&p.AssessmentId==assessmentId&&p.Status=="Draft"
            &&!p.InterventionPackageStudents.Any()&&!p.InterventionPackageResources.Any()).Select(p=>new{p.PackageName,p.Instructions,p.Status}).SingleOrDefaultAsync(ct);
        if(p is null)return null;
        var qs=await db.InterventionPackageQuestions.AsNoTracking().Where(q=>q.InterventionPackageId==packageId).OrderBy(q=>q.QuestionNumber).ToListAsync(ct);
        var ts=await db.InterventionPackageTheories.AsNoTracking().Where(t=>t.InterventionPackageId==packageId).OrderBy(t=>t.SortOrder).ToListAsync(ct);
        if(qs.Count==0||qs.Any(q=>!subskillIds.Contains(q.SnSsubskillId))||ts.Any(t=>!subskillIds.Contains(t.SnSsubskillId)))return null;
        return Fingerprint(new{p,qs,ts});
    }
    public async Task<WorksheetGroupSave> SaveGroupAsync(long assessmentId,long subskillId,WorksheetGroupInput input,CancellationToken ct=default)
    {
        if(!CanWrite)return new(new(false,"Chưa được phép soạn phiếu."));
        input.Theories??=[];input.Questions??=[];input.StudentIds??=[];input.PackageName=input.PackageName?.Trim()??"";
        if(InterventionManagementService.Validate(input) is string error)return new(new(false,error));
        if(!ValidPicks(input.Theories)||!ValidPicks(input.Questions)||input.StudentIds.Any(s=>s<=0)||input.StudentIds.Distinct().Count()!=input.StudentIds.Length)
            return new(new(false,"Danh sách hoặc thứ tự không hợp lệ; không chọn trùng học sinh/nội dung."));
        if(!input.Questions.Any(q=>q.Selected))return new(new(false,"Chọn ít nhất một câu luyện tập."));
        await using var db=await factory.CreateDbContextAsync(ct);
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var page=await GroupCore(db,assessmentId,subskillId,new(){SubskillIds=input.SubskillIds,MatchMode=input.MatchMode},ct);
        if(page is null)return new(new(false,"Không tìm thấy dữ liệu hợp lệ hoặc bạn không có quyền soạn."));
        if(input.Version!=page.Content.Input.Version)return new(new(false,"Kết quả, danh sách lớp hoặc học liệu đã thay đổi. Hãy mở lại trang để kiểm tra."));
        if(input.StudentIds.Any(id=>!page.Students.Any(s=>s.StudentId==id)))return new(new(false,"Chỉ chọn học sinh thuộc lớp có câu sai ở đúng nội dung đang soạn."));
        if(input.Theories.Any(t=>t.Selected&&!page.Content.Theories.Any(x=>x.Id==t.Id))||input.Questions.Any(q=>q.Selected&&!page.Content.Questions.Any(x=>x.Id==q.Id)))
            return new(new(false,"Chỉ chọn lý thuyết và câu luyện đúng nội dung kiến thức đã chọn cho nhóm."));
        var id=await PersistAsync(db,assessmentId,null,input,page.Content,ct);
        var draftVersion=(await DraftVersion(db,id,assessmentId,page.Content.Skills.Select(s=>s.Id).ToArray(),ct))!;
        await tx.CommitAsync(ct);
        return new(new(true,"Đã lưu phiếu nháp. Kiểm tra nội dung và người nhận trước khi xác nhận giao.",id),
            new(id,assessmentId,subskillId,Array.AsReadOnly(input.StudentIds.Order().ToArray()),page.Content.Input.Version,draftVersion){SubskillIds=Array.AsReadOnly(page.Content.Skills.Select(s=>s.Id).Order().ToArray()),MatchMode=input.MatchMode});
    }
    public async Task<WorksheetGroupReview?> GetGroupReviewAsync(WorksheetGroupDraft pending,CancellationToken ct=default)
    {
        if(!CanWrite)return null;
        await using var db=await factory.CreateDbContextAsync(ct);
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,ct);
        await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY",ct);
        var group=await GroupCore(db,pending.AssessmentId,pending.SubskillId,new(){SubskillIds=pending.SubskillIds.Count>0?pending.SubskillIds.ToArray():[pending.SubskillId],MatchMode=pending.MatchMode},ct);
        if(group is null)return null;
        var p=await db.InterventionPackages.AsNoTracking().Where(p=>p.InterventionPackageId==pending.PackageId&&p.AssessmentId==pending.AssessmentId).Select(p=>new{p.PackageName}).SingleOrDefaultAsync(ct);
        if(p is null)return null;
        var version=await DraftVersion(db,pending.PackageId,pending.AssessmentId,pending.SubskillIds.Count>0?pending.SubskillIds:[pending.SubskillId],ct);
        var allowed=version==pending.DraftVersion&&group.Content.Input.Version==pending.BasisVersion&&pending.StudentIds.All(id=>group.Students.Any(s=>s.StudentId==id));
        return new(pending.PackageId,p.PackageName,pending.AssessmentId,pending.SubskillId,
            group.Students.Where(s=>pending.StudentIds.Contains(s.StudentId)).ToArray(),
            await db.InterventionPackageQuestions.CountAsync(q=>q.InterventionPackageId==pending.PackageId,ct),
            await db.InterventionPackageTheories.CountAsync(t=>t.InterventionPackageId==pending.PackageId,ct),allowed,
            allowed?null:"Phiếu, kết quả hoặc nhóm người nhận đã thay đổi. Không tự giao; hãy kiểm tra lại gói và danh sách học sinh.")
            {Skills=group.AvailableSkills.Where(s=>group.Content.Skills.Any(x=>x.Id==s.Id)).ToArray(),MatchMode=pending.MatchMode};
    }
    public Task<InterventionWriteResult> ConfirmGroupAsync(WorksheetGroupDraft pending,CancellationToken ct=default)
    {
        if(!CanWrite)return Task.FromResult(new InterventionWriteResult(false,"Chưa được phép giao phiếu."));
        var management=new InterventionManagementService(factory,interventions,actor,access,options);
        return management.AssignCheckedAsync(pending.PackageId,new(){Ids=pending.StudentIds.ToArray()},async db=>
        {
            if(pending.StudentIds.Count is <1 or >200||pending.StudentIds.Distinct().Count()!=pending.StudentIds.Count)return "Danh sách người nhận không hợp lệ.";
            var group=await GroupCore(db,pending.AssessmentId,pending.SubskillId,new(){SubskillIds=pending.SubskillIds.Count>0?pending.SubskillIds.ToArray():[pending.SubskillId],MatchMode=pending.MatchMode},ct);
            if(group is null||group.Content.Input.Version!=pending.BasisVersion||pending.StudentIds.Any(id=>!group.Students.Any(s=>s.StudentId==id)))
                return "Kết quả, lớp hoặc nhóm người nhận đã thay đổi; chưa giao học sinh nào.";
            if(await DraftVersion(db,pending.PackageId,pending.AssessmentId,pending.SubskillIds.Count>0?pending.SubskillIds:[pending.SubskillId],ct)!=pending.DraftVersion)
                return "Phiếu không còn là bản nháp đã kiểm tra; chưa giao học sinh nào.";
            return null;
        },ct);
    }
}
