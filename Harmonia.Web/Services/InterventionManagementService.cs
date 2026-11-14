using Harmonia.Web.Services.Security;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Harmonia.Web.Data;
using Harmonia.Web.Models.Entities;
using Harmonia.Web.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Harmonia.Web.Services;

public sealed class InterventionManagementOptions
{
    public bool Enabled { get; set; }
}

public sealed class InterventionManagementService(IDbContextFactory<HarmoniaDbContext> factory,
    IOptions<InterventionManagementOptions> options, CurrentActor? actor = null, BusinessAccess? access = null,
    IOptions<WorksheetOptions>? worksheets = null)
{
    public bool CanWrite => options.Value.Enabled;
    private static bool SourceAllowed(string status) => status is "Open" or "Completed";
    internal static string DeliveryVersion(InterventionPackageStudent row) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { row.InterventionPackageStudentId, row.DeliveredDate, row.TeacherNote }))));
    internal static string? Validate(object input)
    {
        var errors = new List<ValidationResult>();
        Validator.TryValidateObject(input, new ValidationContext(input), errors, true);
        return errors.Count > 0 ? string.Join(" ", errors.Select(e => e.ErrorMessage)) : null;
    }
    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static readonly Expression<Func<Assessment, InterventionAssessment>> AssessmentProjection =
        a => new(a.AssessmentId, a.AssessmentName, a.ClassId, a.Class.ClassName, a.AssessmentDate, a.Status);
    private static readonly Expression<Func<InterventionResource, InterventionResourceRow>> ResourceProjection =
        r => new(r.InterventionResourceId, 0, r.ResourceName, r.ResourceUrl, r.Description,
            r.SubskillId, r.Subskill.SubskillCode, r.Subskill.SubskillName, r.Status);

    public async Task<InterventionIndexViewModel> GetIndexAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var rows = await db.InterventionPackages.AsNoTracking().Where(p => db.Assessments.Where(access != null ? access.AssessmentScope() : a => false).Any(a => a.AssessmentId == p.AssessmentId)).OrderByDescending(p => p.InterventionPackageId)
            .Select(p => new InterventionListRow(p.InterventionPackageId, p.PackageName, p.Status,
                new InterventionAssessment(p.AssessmentId, p.Assessment.AssessmentName, p.Assessment.ClassId,
                    p.Assessment.Class.ClassName, p.Assessment.AssessmentDate, p.Assessment.Status),
                p.InterventionPackageResources.Count, p.InterventionPackageStudents.Count)).ToListAsync(ct);
        if (worksheets?.Value.Enabled == true)
        {
            var ids = rows.Select(p => p.PackageId).ToArray();
            var questionCounts = await db.InterventionPackageQuestions.Where(q => ids.Contains(q.InterventionPackageId))
                .GroupBy(q => q.InterventionPackageId).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, ct);
            var theoryCounts = await db.InterventionPackageTheories.Where(t => ids.Contains(t.InterventionPackageId))
                .GroupBy(t => t.InterventionPackageId).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, ct);
            rows = rows.Select(p => p with { WorksheetQuestionCount = questionCounts.GetValueOrDefault(p.PackageId), WorksheetTheoryCount = theoryCounts.GetValueOrDefault(p.PackageId) }).ToList();
        }
        return new(CanWrite, rows);
    }
    public async Task<InterventionCreateViewModel> GetCreateAsync(CreateInterventionInput input, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var assessments = await db.Assessments.AsNoTracking().Where(access != null ? access.AssessmentScope() : a => false).Where(a => actor != null && (actor.IsManager || a.Class.TeacherClassAssignments.Any(t => t.TeacherId == actor.TeacherId && t.StartDate <= a.AssessmentDate && (t.EndDate == null || t.EndDate > a.AssessmentDate)))).Where(a => a.Status == "Open" || a.Status == "Completed")
            .OrderByDescending(a => a.AssessmentDate).ThenBy(a => a.AssessmentId).Select(AssessmentProjection).ToListAsync(ct);
        return new(input, assessments);
    }
    public async Task<InterventionDetailsViewModel?> GetDetailsAsync(long id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        if (access == null || !await access.PackageAsync(id, false, ct)) return null;
        var package = await db.InterventionPackages.AsNoTracking().SingleOrDefaultAsync(p => p.InterventionPackageId == id, ct);
        if (package is null) return null;
        var assessment = await db.Assessments.AsNoTracking().Where(a => a.AssessmentId == package.AssessmentId).Select(AssessmentProjection).SingleAsync(ct);
        var testId = await db.Assessments.Where(a => a.AssessmentId == package.AssessmentId).Select(a => a.TestId).SingleAsync(ct);
        var targets = db.QuestionInTests.Where(q => q.TestId == testId).Select(q => q.SnSsubskillId);
        var linked = await db.InterventionPackageResources.AsNoTracking().Where(r => r.InterventionPackageId == id).OrderBy(r => r.SortOrder)
            .Select(r => new InterventionResourceRow(r.InterventionResourceId, r.SortOrder, r.InterventionResource.ResourceName,
                r.InterventionResource.ResourceUrl, r.InterventionResource.Description, r.InterventionResource.SubskillId,
                r.InterventionResource.Subskill.SubskillCode, r.InterventionResource.Subskill.SubskillName,
                r.InterventionResource.Status)).ToListAsync(ct);
        var available = await db.InterventionResources.AsNoTracking().Where(r => r.Status == "Active" && targets.Contains(r.SubskillId)
            && !db.InterventionPackageResources.Any(pr => pr.InterventionPackageId == id && pr.InterventionResourceId == r.InterventionResourceId))
            .OrderBy(r => r.Subskill.SubskillCode).ThenBy(r => r.ResourceName).Select(ResourceProjection).ToListAsync(ct);
        var assigned = await db.InterventionPackageStudents.AsNoTracking().Where(s => s.InterventionPackageId == id)
            .Include(s => s.Student).OrderBy(s => s.Student.StudentCode).ToListAsync(ct);
        // Same half-open membership rule as S4; no Attempt/score requirement or invented diagnosis threshold.
        var candidates = await EligibleStudents(db, assessment.ClassId, assessment.AssessmentDate)
            .Where(s => !db.InterventionPackageStudents.Any(ps => ps.InterventionPackageId == id && ps.StudentId == s.StudentId))
            .OrderBy(s => s.StudentCode).Select(s => new InterventionCandidate(s.StudentId, s.StudentCode, s.FullName)).ToListAsync(ct);
        var hasWorksheet = worksheets?.Value.Enabled == true && await db.InterventionPackageQuestions.AnyAsync(q => q.InterventionPackageId == id, ct);
        return new(id, package.PackageName, package.Instructions, package.Status, assessment, CanWrite && SourceAllowed(assessment.Status) && await access.AssessmentAsync(assessment.AssessmentId, true, ct),
            linked, available, candidates, assigned.Select(s => new InterventionAssignment(s.InterventionPackageStudentId,
                s.StudentId, s.Student.StudentCode, s.Student.FullName, s.DeliveredDate, s.TeacherNote, DeliveryVersion(s))).ToArray()) { HasInternalWorksheet = hasWorksheet };
    }
    private static IQueryable<Student> EligibleStudents(HarmoniaDbContext db, long classId, DateOnly date) => db.Students.AsNoTracking()
        .Where(s => s.ClassMemberships.Any(m => m.ClassId == classId && m.StartDate <= date && (m.EndDate == null || m.EndDate > date)));

    public async Task<InterventionWriteResult> CreateAsync(CreateInterventionInput input, CancellationToken ct = default)
    {
        if (actor?.Id is null || !CanWrite) return new(false, "Chưa bật chức năng ghi can thiệp.");
        input.PackageName = input.PackageName?.Trim() ?? ""; input.Instructions = Text(input.Instructions);
        if (Validate(input) is string error) return new(false, error);
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var commitStarted = false;
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Assessment\" WHERE \"Assessment_ID\" = {input.AssessmentId} FOR SHARE", ct);
            if (access == null || !await access.AssessmentAsync(input.AssessmentId, true, ct)) return new(false, "Không có quyền ghi can thiệp.");
            var source = await db.Assessments.AsNoTracking().SingleOrDefaultAsync(a => a.AssessmentId == input.AssessmentId, ct);
            if (source is null || !SourceAllowed(source.Status)) return new(false, "Chọn đợt đánh giá nguồn đang mở hoặc đã hoàn tất.");
            var package = new InterventionPackage { AssessmentId = source.AssessmentId, PackageName = input.PackageName,
                Instructions = input.Instructions, Status = "Draft" };
            db.InterventionPackages.Add(package);
            await db.SaveChangesAsync(ct);
            commitStarted = true; await tx.CommitAsync(ct);
            return new(true, "Đã tạo gói nháp. Thêm học liệu trước khi giao học sinh.", package.InterventionPackageId);
        }
        catch (Exception ex) when (ex is DbUpdateException or NpgsqlException or OperationCanceledException)
        {
            try { await tx.RollbackAsync(CancellationToken.None); } catch (Exception rollback) when (rollback is NpgsqlException or InvalidOperationException) { }
            return new(false, commitStarted ? "Chưa xác nhận được kết quả tạo gói. Kiểm tra danh sách trước khi thử lại."
                : "Không lưu được gói. Đợt đánh giá nguồn có thể vừa thay đổi; tải lại trước khi thử lại.");
        }
    }
    public Task<InterventionWriteResult> AttachAsync(long id, InterventionSelectionInput input, CancellationToken ct = default) =>
        WriteAsync(id, async (db, package, source) =>
        {
            if (ValidateSelection(input) is string error) return new(false, error);
            if (package.Status != "Draft" || await db.InterventionPackageStudents.AnyAsync(s => s.InterventionPackageId == id, ct))
                return new(false, "Gói đã giao hoặc không còn là nháp. Tạo gói mới để thay đổi học liệu.");
            if (worksheets?.Value.Enabled == true && await db.InterventionPackageQuestions.AnyAsync(q => q.InterventionPackageId == id, ct))
                return new(false, "Phiếu nội bộ không nhận học liệu URL; hãy sửa phiếu nháp hoặc tạo gói riêng.");
            var ids = input.Ids.Order().ToArray();
            await LockResourcesAsync(db, ids, ct);
            var resources = await db.InterventionResources.AsNoTracking().Where(r => ids.Contains(r.InterventionResourceId)).Select(ResourceProjection).ToListAsync(ct);
            var targets = await db.QuestionInTests.Where(q => q.TestId == source.TestId).Select(q => q.SnSsubskillId).Distinct().ToListAsync(ct);
            if (resources.Count != ids.Length || resources.Any(r => r.Status != "Active" || r.SafeUrl is null || !targets.Contains(r.SubskillId)))
                return new(false, "Học liệu không tồn tại, ngừng hoạt động, URL không hợp lệ hoặc Subskill không thuộc snapshot bài đánh giá nguồn.");
            if (await db.InterventionPackageResources.AnyAsync(r => r.InterventionPackageId == id && ids.Contains(r.InterventionResourceId), ct))
                return new(false, "Học liệu đã có trong gói. Không thêm trùng; tải lại danh sách.");
            var last = await db.InterventionPackageResources.Where(r => r.InterventionPackageId == id).Select(r => (int?)r.SortOrder).MaxAsync(ct) ?? 0;
            if (last + ids.Length > short.MaxValue) return new(false, "Gói đã đạt giới hạn thứ tự học liệu.");
            foreach (var resourceId in ids) db.InterventionPackageResources.Add(new() { InterventionPackageId = id,
                InterventionResourceId = resourceId, SortOrder = (short)++last });
            return new(true, "Đã thêm học liệu và Subskill mục tiêu vào gói.");
        }, ct);

    public Task<InterventionWriteResult> AssignAsync(long id, InterventionSelectionInput input, CancellationToken ct = default) =>
        AssignCheckedAsync(id,input,null,ct);

    internal Task<InterventionWriteResult> AssignCheckedAsync(long id, InterventionSelectionInput input,
        Func<HarmoniaDbContext,Task<string?>>? reviewCheck, CancellationToken ct = default) =>
        WriteAsync(id, async (db, package, source) =>
        {
            if (ValidateSelection(input) is string error) return new(false, error);
            if (reviewCheck is not null && await reviewCheck(db) is string changed) return new(false,changed);
            var resourceIds = await db.InterventionPackageResources.Where(r => r.InterventionPackageId == id).Select(r => r.InterventionResourceId).ToArrayAsync(ct);
            var worksheetTargets = worksheets?.Value.Enabled == true
                ? await db.InterventionPackageQuestions.Where(q => q.InterventionPackageId == id).Select(q => q.SnSsubskillId)
                    .Union(db.InterventionPackageTheories.Where(t => t.InterventionPackageId == id).Select(t => t.SnSsubskillId)).Distinct().ToArrayAsync(ct)
                : [];
            var hasQuestions = worksheets?.Value.Enabled == true && await db.InterventionPackageQuestions.AnyAsync(q => q.InterventionPackageId == id, ct);
            if (resourceIds.Length == 0 && !hasQuestions) return new(false, "Thêm ít nhất một học liệu có Subskill mục tiêu trước khi giao.");
            await LockResourcesAsync(db, resourceIds, ct);
            var resources = await db.InterventionResources.AsNoTracking().Where(r => resourceIds.Contains(r.InterventionResourceId)).Select(ResourceProjection).ToListAsync(ct);
            var targets = await db.QuestionInTests.Where(q => q.TestId == source.TestId).Select(q => q.SnSsubskillId).Distinct().ToListAsync(ct);
            if (worksheetTargets.Any(t => !targets.Contains(t))) return new(false, "Nội dung phiếu không thuộc snapshot đợt đánh giá nguồn.");
            if (resources.Any(r => r.Status != "Active" || r.SafeUrl is null || !targets.Contains(r.SubskillId)))
                return new(false, "Học liệu hoặc Subskill mục tiêu đã thay đổi. Kiểm tra gói trước khi giao thêm học sinh.");
            var ids = input.Ids.Order().ToArray();
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"ClassMembership\" WHERE \"Class_ID\" = {source.ClassId} AND \"Student_ID\" = ANY({ids}) ORDER BY \"ClassMembership_ID\" FOR SHARE", ct);
            var eligible = await EligibleStudents(db, source.ClassId, source.AssessmentDate).CountAsync(s => ids.Contains(s.StudentId), ct);
            if (eligible != ids.Length) return new(false, "Có học sinh không thuộc lớp tại ngày đánh giá nguồn. Không giao một phần danh sách.");
            if (await db.InterventionPackageStudents.AnyAsync(s => s.InterventionPackageId == id && ids.Contains(s.StudentId), ct))
                return new(false, "Có học sinh đã được giao gói này. Không tạo assignment trùng; tải lại danh sách.");
            foreach (var studentId in ids) db.InterventionPackageStudents.Add(new() { InterventionPackageId = id, StudentId = studentId });
            package.Status = "Published";
            return new(true, $"Đã giao gói cho {ids.Length} học sinh. Ngày phát thực tế chưa được ghi; học liệu của gói đã khóa.");
        }, ct, reviewCheck is null ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable);

    public Task<InterventionWriteResult> UpdateDeliveryAsync(long id, long assignmentId, InterventionDeliveryInput input, CancellationToken ct = default) =>
        WriteAsync(id, async (db, package, source) =>
        {
            input.TeacherNote = Text(input.TeacherNote);
            if (Validate(input) is string error) return new(false, error);
            if (input.DeliveredDate is DateOnly date && (date < source.AssessmentDate || date > AcademicManagementService.Today))
                return new(false, "Ngày phát hoặc gửi thực tế phải từ ngày đánh giá ban đầu đến hôm nay. Để trống nếu chưa ghi nhận được ngày; không nhập ngày dự kiến.");
            var row = await db.InterventionPackageStudents.SingleOrDefaultAsync(s => s.InterventionPackageStudentId == assignmentId && s.InterventionPackageId == id, ct);
            if (row is null) return new(false, "Không tìm thấy học sinh được giao trong gói này.");
            if (input.ExpectedVersion != DeliveryVersion(row)) return new(false, "Ngày phát hoặc ghi chú đã được cập nhật ở phiên khác. Tải lại trước khi sửa.");
            row.DeliveredDate = input.DeliveredDate; row.TeacherNote = input.TeacherNote;
            return new(true, "Đã lưu ngày phát thực tế và ghi chú riêng của học sinh.");
        }, ct);

    private static string? ValidateSelection(InterventionSelectionInput input) => Validate(input)
        ?? (input.Ids.Any(i => i <= 0) || input.Ids.Distinct().Count() != input.Ids.Length ? "Danh sách chọn chứa ID không hợp lệ hoặc bị lặp." : null);
    private static Task<int> LockResourcesAsync(HarmoniaDbContext db, long[] ids, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"InterventionResource\" WHERE \"InterventionResource_ID\" = ANY({ids}) ORDER BY \"InterventionResource_ID\" FOR SHARE", ct);

    private async Task<InterventionWriteResult> WriteAsync(long id,
        Func<HarmoniaDbContext, InterventionPackage, Assessment, Task<InterventionWriteResult>> action, CancellationToken ct,
        IsolationLevel isolation = IsolationLevel.ReadCommitted)
    {
        if (actor?.Id is null || !CanWrite) return new(false, "Chưa bật chức năng ghi can thiệp.");
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(isolation, ct);
        var commitStarted = false;
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"InterventionPackage\" WHERE \"InterventionPackage_ID\" = {id} FOR UPDATE", ct);
            var package = await db.InterventionPackages.SingleOrDefaultAsync(p => p.InterventionPackageId == id, ct);
            if (package is null || access == null || !await access.AssessmentAsync(package.AssessmentId, true, ct)) return new(false, "Không tìm thấy gói can thiệp.", NotFound: true);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Assessment\" WHERE \"Assessment_ID\" = {package.AssessmentId} FOR SHARE", ct);
            var source = await db.Assessments.AsNoTracking().SingleAsync(a => a.AssessmentId == package.AssessmentId, ct);
            if (!SourceAllowed(source.Status) || package.Status is not ("Draft" or "Published"))
                return new(false, "Gói đã lưu trữ hoặc đợt đánh giá nguồn không còn được phép ghi can thiệp.");
            // Preserve the target snapshot while validating resource links and assignments.
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"QuestionInTest\" WHERE \"Test_ID\" = {source.TestId} ORDER BY \"QuestionInTest_ID\" FOR SHARE", ct);
            var result = await action(db, package, source);
            if (!result.Succeeded) return result; // Disposal rolls back; no SaveChanges before all validations succeed.
            await db.SaveChangesAsync(ct);
            commitStarted = true; await tx.CommitAsync(ct);
            return result;
        }
        catch (Exception ex) when (ex is DbUpdateException or NpgsqlException or OperationCanceledException)
        {
            try { await tx.RollbackAsync(CancellationToken.None); } catch (Exception rollback) when (rollback is NpgsqlException or InvalidOperationException) { }
            return new(false, commitStarted
                ? "Chưa xác nhận được kết quả lưu. Tải lại gói để kiểm tra trước khi thử lại."
                : "Không lưu được thay đổi. Có dữ liệu vừa thay đổi hoặc quan hệ bị xung đột; không lưu một phần. Tải lại gói để kiểm tra.");
        }
    }
}
