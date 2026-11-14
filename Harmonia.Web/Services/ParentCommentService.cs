using System.Data;
using Harmonia.Web.Data;
using Harmonia.Web.Models.Entities;
using Harmonia.Web.Services.Security;
using Harmonia.Web.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Harmonia.Web.Services;

public sealed partial class ParentCommentService(IDbContextFactory<HarmoniaDbContext> factory, BusinessAccess access,
    CurrentActor actor, IOptions<ParentCommentOptions> options, IOptions<WorksheetOptions> worksheets)
{
    public bool Enabled => options.Value.Enabled && actor.Id is not null;
    private StudentReportService Reports(HarmoniaDbContext db) => new(db, new(db), worksheets);

    public async Task<ParentCommentEditor?> GetAsync(long assessmentId, long studentId, CancellationToken ct = default)
    {
        if (!Enabled || assessmentId <= 0 || studentId <= 0) return null;
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", ct);
        var classId = await db.Assessments.AsNoTracking().Where(access.AssessmentScope()).Where(a => a.AssessmentId == assessmentId)
            .Select(a => (long?)a.ClassId).SingleOrDefaultAsync(ct);
        if (!classId.HasValue) return null;
        var report = await Reports(db).BuildBaseAsync(assessmentId, studentId, ct);
        if (report is null) return null;
        var canWrite = await db.Assessments.Where(access.AssessmentWriteScope()).AnyAsync(a => a.AssessmentId == assessmentId, ct);
        var observations = await ParentCommentEvidence.EligibleObservationsAsync(db, classId.Value, studentId, ct);
        var pair = db.StudentReportComments.AsNoTracking().Where(c => c.AssessmentId == assessmentId && c.StudentId == studentId);
        var active = await pair.Where(c => c.Status == "Draft" || c.Status == "Approved").Select(c => new CommentMeta(c.StudentReportCommentId,
            c.RevisionNumber, c.Status, c.CommentText, c.LearningObservationId, c.RowVersion, c.CreatedByUserId, c.InputFingerprint, c.InputSchemaVersion, c.ApprovedAt, c.Source)).ToArrayAsync(ct);
        var history = await pair.Where(c => c.Status != "Draft" && c.Status != "Approved").OrderByDescending(c => c.RevisionNumber).Take(20)
            .Select(c => new CommentMeta(c.StudentReportCommentId, c.RevisionNumber, c.Status, c.CommentText, c.LearningObservationId,
                c.RowVersion, c.CreatedByUserId, c.InputFingerprint, c.InputSchemaVersion, c.ApprovedAt, c.Source)).ToArrayAsync(ct);
        var sections = options.Value.SeparateSections ? await CommentSectionStorage.ReadAsync(db,assessmentId,studentId,ct) : [];
        var users = observations.Select(o => o.CreatedByUserId).Concat(active.Select(c => c.Actor)).Concat(history.Select(c => c.Actor)).Distinct().ToArray();
        var names = await db.Database.SqlQuery<ObservationAuthor>($"""
            SELECT u."Id" AS "Id",COALESCE(t."FullName",u."UserName",'Người ghi nhận') AS "Name"
            FROM auth."AspNetUsers" u LEFT JOIN auth."TeacherUserLinks" l ON l."User_ID"=u."Id"
            LEFT JOIN public."Teacher" t ON t."Teacher_ID"=l."Teacher_ID" WHERE u."Id"=ANY({users})
            """).ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        async Task<CommentRevision?> Active(string status)
        {
            var c = active.SingleOrDefault(c => c.Status == status); if (c is null) return null;
            var evidence = await ParentCommentEvidence.CaptureAsync(db, report, c.Observation, ct);
            return Revision(c, evidence is null || c.Schema != 1 || ParentCommentEvidence.Fingerprint(evidence.Data) != c.Fingerprint);
        }
        CommentRevision Revision(CommentMeta c, bool stale = false) => new(c.Id, c.Revision, c.Status, c.Text, c.Observation, c.Version,
            names.GetValueOrDefault(c.Actor, "Người ghi nhận"), canWrite && (actor.IsManager || c.Actor == actor.Id) && c.Status == "Draft", stale, c.Approved) { Source=c.Source, ProcessText=sections.SingleOrDefault(s=>s.Id==c.Id)?.ProcessCommentText,
                SectionsSeparated=sections.SingleOrDefault(s=>s.Id==c.Id)?.ProcessCommentText is not null };
        var draft = await Active("Draft"); var approved = await Active("Approved");
        var choices = observations.Select(o => new CommentObservationChoice(o.LearningObservationId, o.PeriodStart, o.PeriodEnd,
            names.GetValueOrDefault(o.CreatedByUserId, "Người ghi nhận"), o.TaskCompletion, o.Participation, o.Preparation, o.Cooperation)).ToArray();
        return new(report, canWrite, choices, draft, approved, history.Select(c => Revision(c)).ToArray(),
            new() { DraftId = draft?.Id, ExpectedVersion = draft?.Version, ExpectedApprovedId = approved?.Id,
                ObservationId = draft is not null ? draft.ObservationId : approved?.ObservationId, Text = draft is not null ? draft.Text : approved?.Text ?? "",
                ProcessText=draft is not null ? draft.ProcessText : approved?.ProcessText ?? "" }, []);
    }

    public Task<ParentCommentWriteResult> SaveAsync(long assessmentId, long studentId, SaveParentCommentInput input, CancellationToken ct = default) =>
        WriteAsync(assessmentId, studentId, "Save", input, null, ct);
    public Task<ParentCommentWriteResult> ApproveAsync(long assessmentId, long studentId, ParentCommentActionInput input, CancellationToken ct = default) =>
        WriteAsync(assessmentId, studentId, "Approve", null, input, ct);
    public Task<ParentCommentWriteResult> DiscardAsync(long assessmentId, long studentId, ParentCommentActionInput input, CancellationToken ct = default) =>
        WriteAsync(assessmentId, studentId, "Discard", null, input, ct);

    private async Task<ParentCommentWriteResult> WriteAsync(long assessmentId, long studentId, string operation,
        SaveParentCommentInput? input, ParentCommentActionInput? action, CancellationToken ct, Generation? generated = null)
    {
        if (!Enabled || assessmentId <= 0 || studentId <= 0) return Missing();
        if (input is not null)
        {
            input.Text = (input.Text ?? "").Trim();
            input.ProcessText=(input.ProcessText??"").Trim();
            if(input.ProcessText.Length>10000)return new(false,"Ghi nhận học tập tối đa 10.000 ký tự.");
            if(!options.Value.SeparateSections && input.ProcessText.Length>0)return new(false,"Chức năng hai đoạn nhận xét chưa được bật.");
            if (input.Text.Length > 10000) return new(false, "Nhận xét tối đa 10.000 ký tự.");
            if (input.DraftId is <= 0 || input.ObservationId is <= 0 || input.ExpectedVersion is <= 0 || input.ExpectedApprovedId is <= 0)
                return new(false, "Thông tin bản nháp không hợp lệ. Tải bản mới nhất rồi thử lại.");
        }
        if (action is not null && (action.DraftId <= 0 || action.ExpectedVersion <= 0 || !action.Confirmed))
            return new(false, "Hãy kiểm tra và đánh dấu xác nhận trước khi thực hiện.");
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var classId = await db.Assessments.Where(access.AssessmentWriteScope()).Where(a => a.AssessmentId == assessmentId)
            .Select(a => (long?)a.ClassId).SingleOrDefaultAsync(ct);
        if (!classId.HasValue) return Missing();
        // Class first, then Assessment, consistent with observation/roster writers. No Package locks:
        // S9 locks Package then Assessment, so taking Package here would invert its order.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM public.\"Class\" WHERE \"Class_ID\"={classId.Value} FOR UPDATE", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM public.\"Assessment\" WHERE \"Assessment_ID\"={assessmentId} FOR UPDATE", ct);
        if (!actor.IsManager)
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM public.\"TeacherClassAssignment\" WHERE \"Class_ID\"={classId.Value} AND \"Teacher_ID\"={actor.TeacherId} ORDER BY \"TeacherClassAssignment_ID\" FOR SHARE", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM public.\"ClassMembership\" WHERE \"Class_ID\"={classId.Value} AND \"Student_ID\"={studentId} ORDER BY \"ClassMembership_ID\" FOR SHARE", ct);
        if (!await db.Assessments.Where(access.AssessmentWriteScope()).AnyAsync(a => a.AssessmentId == assessmentId, ct)) return Missing();
        var report = await Reports(db).BuildBaseAsync(assessmentId, studentId, ct);
        if (report is null) return Missing();
        var pair = db.StudentReportComments.Where(c => c.AssessmentId == assessmentId && c.StudentId == studentId);
        var active = await pair.Where(c => c.Status == "Draft" || c.Status == "Approved").ToListAsync(ct);
        var draft = active.SingleOrDefault(c => c.Status == "Draft"); var approved = active.SingleOrDefault(c => c.Status == "Approved");
        var storedSections=options.Value.SeparateSections ? (await CommentSectionStorage.ReadAsync(db,assessmentId,studentId,ct)).SingleOrDefault(s=>s.Id==draft?.StudentReportCommentId) : null;
        string? processToWrite=null; string? aiProcessToWrite=storedSections?.AiProcessDraftText;
        if (generated is not null)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM auth.\"AspNetUsers\" WHERE \"Id\"={actor.RequiredId} FOR SHARE", ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM auth.\"AspNetUserRoles\" ur JOIN auth.\"AspNetRoles\" r ON r.\"Id\"=ur.\"RoleId\" WHERE ur.\"UserId\"={actor.RequiredId} FOR SHARE", ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM auth.\"TeacherUserLinks\" WHERE \"User_ID\"={actor.RequiredId} FOR SHARE", ct);
            if (await AuthStampAsync(db, ct) != generated.AuthStamp || draft is null || !Owns(draft)
                || draft.StudentReportCommentId != action!.DraftId || draft.RowVersion != action.ExpectedVersion
                || approved?.StudentReportCommentId != generated.ApprovedId || draft.BaseApprovedCommentId != generated.ApprovedId)
                return Conflict();
            var evidence = await CaptureForWrite(draft.LearningObservationId);
            if (evidence is null || ParentCommentEvidence.Fingerprint(evidence.Data) != generated.Fingerprint
                || draft.InputFingerprint != generated.Fingerprint) return Conflict();
            draft.CommentText = generated.Text; draft.AiDraftText = generated.Text;
            draft.Source = "Gemini"; draft.AiModel = generated.Model; draft.PromptVersion = generated.PromptVersion;
            if(options.Value.SeparateSections){processToWrite=generated.ProcessText??"";aiProcessToWrite=processToWrite;}
            draft.GeneratedAt = DateTime.UtcNow; Touch(draft);
        }
        else if (operation == "Save")
        {
            if (input!.ExpectedApprovedId != approved?.StudentReportCommentId) return Conflict();
            if (input.DraftId.HasValue)
            {
                if (draft is null || draft.StudentReportCommentId != input.DraftId || draft.RowVersion != input.ExpectedVersion) return Conflict();
                if (!Owns(draft)) return new(false, "Bản nháp do giáo viên khác tạo. Quản lý có thể hủy nháp nếu cần bàn giao.");
                if (draft.BaseApprovedCommentId != approved?.StudentReportCommentId) return Conflict();
            }
            else if (draft is not null || input.ExpectedVersion.HasValue) return Conflict();
            var evidence = await CaptureForWrite(input.ObservationId);
            if (evidence is null) return new(false, "Ghi nhận không phù hợp với học sinh/lớp hoặc thời gian tham gia. Chọn lại ghi nhận hoặc không sử dụng ghi nhận.");
            if(options.Value.SeparateSections && !string.IsNullOrEmpty(input.ProcessText) && !HasProcessEvidence(evidence.Data))
                return new(false,"Chọn ghi nhận có tiêu chí đã ghi nhận trước khi viết nhận xét quá trình học tập.");
            if(options.Value.SeparateSections && draft is {Source:"Gemini"} && storedSections?.ProcessCommentText is null)
                return new(false,"Nháp AI cũ chưa tách hai phần. Tạo lại gợi ý hoặc hủy nháp rồi soạn bản mới.",Conflict:true);
            var fingerprint = ParentCommentEvidence.Fingerprint(evidence.Data);
            if (draft is { Source: "Gemini" } && (draft.InputFingerprint != fingerprint || draft.LearningObservationId != input.ObservationId))
                return new(false, "Căn cứ của nháp AI đã thay đổi. Hủy nháp rồi tạo bản mới; nguồn gợi ý cũ được giữ trong lịch sử.", Conflict: true);
            if (draft is not null && draft.CommentText == input.Text && draft.LearningObservationId == input.ObservationId
                && draft.InputFingerprint == fingerprint && (!options.Value.SeparateSections || storedSections?.ProcessCommentText==input.ProcessText)) return new(true, "Bản nháp không thay đổi; dữ liệu đã lưu được giữ nguyên.");
            if (draft is null)
            {
                var revision = (await pair.MaxAsync(c => (int?)c.RevisionNumber, ct) ?? 0) + 1;
                draft = new() { StudentId = studentId, AssessmentId = assessmentId, RevisionNumber = revision,
                    CreatedByUserId = actor.RequiredId, BaseApprovedCommentId = approved?.StudentReportCommentId };
                db.StudentReportComments.Add(draft);
            }
            else Touch(draft);
            if(options.Value.SeparateSections)processToWrite=input.ProcessText??"";
            draft.CommentText = input.Text ?? ""; draft.LearningObservationId = input.ObservationId;
            draft.InputSchemaVersion = 1; draft.InputSnapshot = ParentCommentEvidence.Serialize(evidence); draft.InputFingerprint = fingerprint;
        }
        else
        {
            if (draft is null || draft.StudentReportCommentId != action!.DraftId || draft.RowVersion != action.ExpectedVersion) return Conflict();
            if (!Owns(draft)) return new(false, "Bạn chỉ được quản lý bản nháp do mình tạo.");
            if (operation == "Discard") { draft.Status = "Discarded"; Touch(draft); }
            else
            {
                if (draft.BaseApprovedCommentId != approved?.StudentReportCommentId) return Conflict();
                if(options.Value.SeparateSections && storedSections?.ProcessCommentText is null)
                    return new(false,"Kiểm tra và lưu hai phần nhận xét trước khi duyệt.");
                if (string.IsNullOrWhiteSpace(draft.CommentText)) return new(false, "Nhập nhận xét trước khi duyệt.");
                var evidence = await CaptureForWrite(draft.LearningObservationId);
                if (evidence is null || draft.InputSchemaVersion != 1 || ParentCommentEvidence.Fingerprint(evidence.Data) != draft.InputFingerprint)
                    return new(false, "Căn cứ đã thay đổi. Kiểm tra và lưu lại bản nháp trước khi duyệt.", Conflict: true);
                if (approved is not null)
                {
                    approved.Status = "Superseded"; Touch(approved);
                    // Release partial-UNIQUE Approved slot inside this transaction; rollback restores it on failure.
                    await db.SaveChangesAsync(ct);
                }
                draft.InputSnapshot = ParentCommentEvidence.Serialize(evidence);
                draft.Status = "Approved"; draft.ApprovedByUserId = actor.RequiredId; draft.ApprovedAt = DateTime.UtcNow; Touch(draft);
            }
        }
        try { await db.SaveChangesAsync(ct);
            if(processToWrite is not null)await CommentSectionStorage.WriteAsync(db,draft!.StudentReportCommentId,draft.RowVersion,processToWrite,aiProcessToWrite,ct);
            await tx.CommitAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict(); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" }) { return Conflict(); }
        return new(true, operation == "Generate" ? "Đã lưu gợi ý AI vào bản nháp. Hãy kiểm tra, chỉnh sửa và duyệt trước khi gửi phụ huynh."
            : operation == "Save" ? "Đã lưu bản nháp. Chưa đưa nhận xét vào báo cáo phụ huynh."
            : operation == "Discard" ? "Đã hủy bản nháp; bản đã duyệt được giữ nguyên." : "Đã duyệt nhận xét. Có thể mở báo cáo để in / lưu PDF.");

        bool Owns(StudentReportComment c) => actor.IsManager || c.CreatedByUserId == actor.Id;
        void Touch(StudentReportComment c) { c.RowVersion++; c.UpdatedAt = DateTime.UtcNow; c.UpdatedByUserId = actor.RequiredId; }
        async Task<CommentEvidenceSnapshot?> CaptureForWrite(long? observationId)
        {
            if (observationId.HasValue)
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM public.\"LearningObservation\" WHERE \"LearningObservation_ID\"={observationId.Value} AND \"Student_ID\"={studentId} AND \"Class_ID\"={classId.Value} FOR SHARE", ct);
            return await ParentCommentEvidence.CaptureAsync(db, report, observationId, ct);
        }
    }
    private static bool HasProcessEvidence(CommentSemanticEvidence e) => e.Observation is {} o && new[]{o.Task,o.Participation,o.Preparation,o.Cooperation}.Any(x=>x is not null);
    private static ParentCommentWriteResult Missing() => new(false, "Dữ liệu không tồn tại hoặc ngoài phạm vi quyền.", NotFound: true);
    private static ParentCommentWriteResult Conflict() => new(false, "Bản nhận xét hoặc căn cứ đã thay đổi. Tải bản mới nhất trước khi tiếp tục.", Conflict: true);
    private sealed record CommentMeta(long Id, int Revision, string Status, string Text, long? Observation,
        long Version, string Actor, string Fingerprint, short Schema, DateTime? Approved, string Source);
}
