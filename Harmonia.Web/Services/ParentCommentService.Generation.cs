using System.Data;
using Harmonia.Web.Data;
using Harmonia.Web.Services.Gemini;
using Harmonia.Web.ViewModels;
using Microsoft.EntityFrameworkCore;
namespace Harmonia.Web.Services;

public sealed partial class ParentCommentService
{
    private sealed record Generation(string Text,string Model,string Fingerprint,long? ApprovedId,string AuthStamp,string? ProcessText,string PromptVersion);
    public async Task<ParentCommentWriteResult> GenerateAsync(long assessmentId,long studentId,ParentCommentActionInput input,
        GeminiCommentClient client,string model,CancellationToken ct)
    {
        if(!Enabled || assessmentId<=0 || studentId<=0)return Missing();
        if(!input.Confirmed || input.DraftId<=0 || input.ExpectedVersion<=0)return new(false,"Lưu nháp và xác nhận thay nội dung trước khi tạo gợi ý.");
        CommentEvidenceSnapshot evidence;string stamp;long? approved;
        await using(var db=await factory.CreateDbContextAsync(ct))
        {
            await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,ct);
            await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY",ct);
            if(!await db.Assessments.Where(access.AssessmentWriteScope()).AnyAsync(a=>a.AssessmentId==assessmentId,ct))return Missing();
            var draft=await db.StudentReportComments.AsNoTracking().SingleOrDefaultAsync(c=>c.StudentReportCommentId==input.DraftId
                &&c.AssessmentId==assessmentId&&c.StudentId==studentId&&c.Status=="Draft",ct);
            if(draft is null || draft.RowVersion!=input.ExpectedVersion || !(actor.IsManager||draft.CreatedByUserId==actor.Id))return Conflict();
            approved=await db.StudentReportComments.Where(c=>c.AssessmentId==assessmentId&&c.StudentId==studentId&&c.Status=="Approved").Select(c=>(long?)c.StudentReportCommentId).SingleOrDefaultAsync(ct);
            if(draft.BaseApprovedCommentId!=approved)return Conflict();
            var report=await Reports(db).BuildBaseAsync(assessmentId,studentId,ct);
            var captured=report is null?null:await ParentCommentEvidence.CaptureAsync(db,report,draft.LearningObservationId,ct);
            if(captured is null || ParentCommentEvidence.Fingerprint(captured.Data)!=draft.InputFingerprint)return Conflict();
            var auth=await AuthStampAsync(db,ct);if(auth is null)return Missing();stamp=auth;evidence=captured;
        }
        // No connection, transaction or business locks held during generation.
        var response=await client.GenerateAsync(GeminiPayload.From(evidence.Data),ct);
        if(response.Succeeded && options.Value.SeparateSections && (response.PromptVersion!=GeminiCommentClient.SectionsPromptVersion || response.ProcessText is null))
            return new(false,"Cấu hình gợi ý chưa phù hợp với hai phần nhận xét. Bản nháp được giữ nguyên.");
        if(!response.Succeeded)return new(false,"Chưa tạo được gợi ý ("+response.Code+"). Bản nháp đã lưu được giữ nguyên.");
        if(ct.IsCancellationRequested)return new(false,"Đã hủy tạo gợi ý; bản nháp được giữ nguyên.");
        return await WriteAsync(assessmentId,studentId,"Generate",null,input,ct,
            new(response.Text!,model,ParentCommentEvidence.Fingerprint(evidence.Data),approved,stamp,response.ProcessText,response.PromptVersion));
    }
    private async Task<string?> AuthStampAsync(HarmoniaDbContext db,CancellationToken ct)
    {
        var role=actor.IsManager?"AcademicManager":"Teacher";
        var teacher=actor.TeacherId??0;
        return await db.Database.SqlQuery<string>($"""
            SELECT jsonb_build_array(u."SecurityStamp",u."ConcurrencyStamp",
              (SELECT string_agg(r."Name",',' ORDER BY r."Name") FROM auth."AspNetUserRoles" ur JOIN auth."AspNetRoles" r ON r."Id"=ur."RoleId" WHERE ur."UserId"=u."Id"))::text AS "Value"
            FROM auth."AspNetUsers" u WHERE u."Id"={actor.RequiredId} AND u."IsActive" AND NOT u."MustChangePassword"
              AND (u."LockoutEnd" IS NULL OR u."LockoutEnd"<=now())
              AND EXISTS(SELECT 1 FROM auth."AspNetUserRoles" ur JOIN auth."AspNetRoles" r ON r."Id"=ur."RoleId" WHERE ur."UserId"=u."Id" AND r."Name"={role})
              AND ({actor.IsManager} OR EXISTS(SELECT 1 FROM auth."TeacherUserLinks" l WHERE l."User_ID"=u."Id" AND l."Teacher_ID"={teacher}))
            """).SingleOrDefaultAsync(ct);
    }
}
