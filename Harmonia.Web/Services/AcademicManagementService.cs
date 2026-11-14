using Harmonia.Web.Services.Security;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Harmonia.Web.Data;
using Harmonia.Web.Models.Entities;
using Harmonia.Web.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Harmonia.Web.Services;

public sealed class AcademicManagementOptions
{
    public bool Enabled { get; set; }
    public string DemoActorId { get; set; } = "harmonia-demo-academic";
    public bool CanWrite => Enabled;
}

public sealed class AcademicManagementService(IDbContextFactory<HarmoniaDbContext> factory,
    IOptions<AcademicManagementOptions> options, CurrentActor? actor = null, BusinessAccess? access = null)
{
    public bool CanWrite => options.Value.Enabled && actor?.IsManager == true;
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
    internal static Task<AcademicClassHeader?> ReadHeaderAsync(HarmoniaDbContext db, long id, CancellationToken ct) => db.Classes.AsNoTracking()
        .Where(c => c.ClassId == id)
        .Select(c => new AcademicClassHeader(c.ClassId, c.ClassCode, c.ClassName, c.Grade, c.AcademicYear, c.Status)).SingleOrDefaultAsync(ct);

    public async Task<AcademicClassListViewModel> GetClassesAsync(CancellationToken ct = default, bool mine = false)
    {
        var today = Today;
        if (actor is null || access is null) return new(today, CanWrite, []);
        await using var db = await factory.CreateDbContextAsync(ct);
        var teacherId = actor.TeacherId;
        var rows = await db.Classes.AsNoTracking().Where(access.ClassScope())
            .Where(c => !mine || teacherId.HasValue && c.TeacherClassAssignments.Any(a => a.TeacherId == teacherId
                && a.StartDate <= today && (a.EndDate == null || a.EndDate > today))).OrderByDescending(c => c.AcademicYear).ThenBy(c => c.ClassName)
            .Select(c => new AcademicClassListRow(new(c.ClassId, c.ClassCode, c.ClassName, c.Grade, c.AcademicYear, c.Status),
                c.ClassMemberships.Where(m => m.StartDate <= today && (m.EndDate == null || m.EndDate > today))
                    .Select(m => m.StudentId).Distinct().Count())).ToListAsync(ct);
        var ids = rows.Select(r => r.Class.ClassId).ToArray();
        var byClass = new Dictionary<long, ClassTeacherAssignmentRow[]>();
        if (ids.Length > 0)
        {
            var teachers = await db.TeacherClassAssignments.AsNoTracking()
                .Where(a => ids.Contains(a.ClassId) && (actor.IsManager && !mine || a.TeacherId == actor.TeacherId)
                    && a.StartDate <= today && (a.EndDate == null || a.EndDate > today))
                .OrderBy(a => a.Teacher.FullName).Select(a => new { a.ClassId,
                    Row = new ClassTeacherAssignmentRow(a.TeacherId, a.Teacher.TeacherCode, a.Teacher.FullName, a.StartDate, a.EndDate, true) }).ToListAsync(ct);
            byClass = teachers.GroupBy(a => a.ClassId).ToDictionary(g => g.Key, g => g.Select(a => a.Row).DistinctBy(a => a.TeacherId).ToArray());
        }
        return new(today, CanWrite, rows.Select(r => r with { Teachers = byClass.TryGetValue(r.Class.ClassId, out var items) ? items : [] }).ToArray()) {
            MineOnly = mine, CanViewMine = actor.CanTeach
        };
    }
    public async Task<ClassDetailsViewModel?> GetClassAsync(long id, DateOnly? asOf = null, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        if (access is null || actor is null || !await db.Classes.Where(access.ClassScope()).AnyAsync(c => c.ClassId == id, ct)) return null;
        var header = await ReadHeaderAsync(db, id, ct);
        if (header is null) return null;
        var date = asOf ?? Today;
        var students = await db.ClassMemberships.AsNoTracking()
            .Where(m => m.ClassId == id && m.StartDate <= date && (m.EndDate == null || m.EndDate > date))
            .OrderBy(m => m.Student.StudentCode)
            .Select(m => new ClassStudentRow(m.StudentId, m.Student.StudentCode, m.Student.FullName,
                m.Student.DateOfBirth, m.Student.Gender, m.Student.Status, m.StartDate, m.EndDate)).ToListAsync(ct);
        var teachers = await db.TeacherClassAssignments.AsNoTracking()
            .Where(a => a.ClassId == id && (actor.IsManager || a.TeacherId == actor.TeacherId)
                && a.StartDate <= date && (a.EndDate == null || a.EndDate > date))
            .OrderBy(a => a.Teacher.FullName)
            .Select(a => new ClassTeacherAssignmentRow(a.TeacherId, a.Teacher.TeacherCode, a.Teacher.FullName, a.StartDate, a.EndDate, true)).ToListAsync(ct);
        var assessments = await db.Assessments.AsNoTracking().Where(access.AssessmentScope()).Where(a => a.ClassId == id)
            .OrderByDescending(a => a.AssessmentDate).ThenByDescending(a => a.AssessmentId)
            .Select(a => new ClassAssessmentRow(a.AssessmentId, a.AssessmentName, a.Test.TestName, a.AssessmentDate, a.Status)).ToListAsync(ct);
        return new(header, date, CanWrite && header.Status == "Active", students) {
            Teachers = teachers.DistinctBy(a => a.TeacherId).ToArray(), Assessments = assessments,
            CanAssignTeacher = actor.IsManager && header.Status == "Active"
        };
    }
    internal static IReadOnlyList<ImportError> ValidateClass(CreateClassInput input)
    {
        input.ClassCode = input.ClassCode?.Trim() ?? "";
        input.ClassName = input.ClassName?.Trim() ?? "";
        input.AcademicYear = input.AcademicYear?.Trim() ?? "";
        var validation = new List<ValidationResult>();
        Validator.TryValidateObject(input, new ValidationContext(input), validation, true);
        var errors = validation.Select(v => new ImportError(null, string.Join(',', v.MemberNames), v.ErrorMessage!)).ToList();
        if (!Regex.IsMatch(input.AcademicYear, "^[0-9]{4}-[0-9]{4}$")
            || int.Parse(input.AcademicYear.AsSpan(5)) != int.Parse(input.AcademicYear.AsSpan(0, 4)) + 1)
            errors.Add(new(null, nameof(input.AcademicYear), "Năm học phải gồm hai năm liên tiếp, ví dụ 2026-2027."));
        if (input.ClassCode.Any(char.IsControl) || input.ClassName.Any(char.IsControl))
            errors.Add(new(null, "Class", "Mã và tên lớp không được chứa ký tự điều khiển."));
        return errors;
    }
    public async Task<ClassCreation> CreateAsync(CreateClassInput input, CancellationToken ct = default)
    {
        if (actor?.IsManager != true || !CanWrite) return new(null, new[] { new ImportError(null, "Class", "Chưa bật chức năng ghi dữ liệu học thuật cho môi trường này.") });
        var errors = ValidateClass(input);
        if (errors.Count > 0) return new(null, errors);
        await using var db = await factory.CreateDbContextAsync(ct);
        if (await db.Classes.AnyAsync(c => c.ClassCode == input.ClassCode && c.AcademicYear == input.AcademicYear, ct))
            return DuplicateClass();
        var entity = new Class { ClassCode = input.ClassCode, ClassName = input.ClassName,
            Grade = input.Grade, AcademicYear = input.AcademicYear, Status = "Active",
            CreatedAt = DateTime.UtcNow, CreatedByUserId = actor!.RequiredId };
        db.Classes.Add(entity);
        try { await db.SaveChangesAsync(ct); return new(entity.ClassId, Array.Empty<ImportError>()); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { return DuplicateClass(); }
    }
    private static ClassCreation DuplicateClass() => new(null,
        new[] { new ImportError(null, nameof(CreateClassInput.ClassCode), "Mã lớp đã tồn tại trong năm học này.") });

    public async Task<ClassDeletion> DeleteAsync(long id, string expectedName, CancellationToken ct = default)
    {
        if (actor?.IsManager != true || !CanWrite) return new(false, false, "Chưa bật chức năng ghi dữ liệu học thuật cho môi trường này.");
        await using var db = await factory.CreateDbContextAsync(ct);
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
            // FK inserts take KEY SHARE on Class. FOR UPDATE serializes them with this check/delete.
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Class\" WHERE \"Class_ID\" = {id} FOR UPDATE", ct);
            var header = await ReadHeaderAsync(db, id, ct);
            if (header is null) return new(false, true, "Lớp không còn tồn tại. Không thực hiện xóa.");
            if (!string.Equals(header.ClassName, expectedName, StringComparison.Ordinal))
                return new(false, false, "Tên lớp đã thay đổi. Tải lại danh sách và xác nhận đúng tên lớp trước khi xóa.");
            var reasons = new List<string>();
            // Check ALL history, not only memberships/assignments active today or the displayed roster count.
            if (await db.ClassMemberships.AnyAsync(m => m.ClassId == id, ct)) reasons.Add("thành viên lớp (ClassMembership), kể cả lịch sử/quá hạn");
            if (await db.Assessments.AnyAsync(a => a.ClassId == id, ct)) reasons.Add("đợt đánh giá (Assessment)");
            if (await db.TeacherClassAssignments.AnyAsync(a => a.ClassId == id, ct)) reasons.Add("phân công giáo viên (TeacherClassAssignment), kể cả lịch sử");
            if (reasons.Count > 0) return new(false, false, "Không thể xóa lớp vì đã có " + string.Join("; ", reasons) + ". Không xóa dữ liệu liên quan.");
            // Execute a single Class DELETE; do not load/remove navigation collections or cascade in EF.
            await db.Classes.Where(c => c.ClassId == id).ExecuteDeleteAsync(ct);
            await tx.CommitAsync(ct);
            return new(true, false, $"Đã xóa lớp \"{header.ClassName}\".");
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            return new(false, false, "Không thể xóa lớp vì có dữ liệu liên quan đang tham chiếu đến lớp. Tải lại danh sách để kiểm tra. Không xóa dữ liệu liên quan.");
        }
    }
}
