using Harmonia.Web.Services.Security;
using System.Data;
using System.Text;
using Harmonia.Web.Data;
using Harmonia.Web.Models.Entities;
using Harmonia.Web.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Harmonia.Web.Services.Academic;

public sealed class StudentRosterImportService(IDbContextFactory<HarmoniaDbContext> factory,
    StudentRosterParser parser, RosterPreviewStore store, IOptions<AcademicManagementOptions> options,
    ILogger<StudentRosterImportService> logger, CurrentActor? actor = null)
{
    private bool Allowed(AcademicClassHeader header) => actor?.IsManager == true && options.Value.CanWrite && header.Status == "Active";
    public async Task<RosterImportPage?> GetPageAsync(long classId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var header = await AcademicManagementService.ReadHeaderAsync(db, classId, ct);
        return header is null || actor?.IsManager != true ? null : new(header, Allowed(header), actor.RequiredId, Array.Empty<ImportError>());
    }
    public async Task<CsvTemplateDownload?> GetTemplateAsync(long classId, CancellationToken ct = default)
    {
        var page = await GetPageAsync(classId, ct);
        if (page is null || !page.CanImport) return null;
        var name = new string(page.Class.ClassCode.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-').ToArray());
        return new(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(StudentRosterParser.Header + "\r\n")).ToArray(),
            $"Harmonia_Danh-sach_{name}_{page.Class.AcademicYear}.csv");
    }
    internal static IReadOnlyList<ImportError> MatchExisting(RosterCsvRow row, Student student,
        IEnumerable<ClassMembership> memberships)
    {
        var errors = new List<ImportError>();
        if (!string.Equals(row.FullName.Trim(), student.FullName.Trim(), StringComparison.OrdinalIgnoreCase)
            || row.DateOfBirth != student.DateOfBirth || row.Gender != student.Gender)
            errors.Add(new(row.Line, "StudentCode", "Mã học sinh đã tồn tại nhưng họ tên, ngày sinh hoặc giới tính không khớp hồ sơ. Hãy kiểm tra lại thông tin trong tệp. Chưa nhập học sinh nào từ tệp này."));
        if (memberships.Any(m => StudentRosterParser.Overlaps(row.StartDate, row.EndDate, m.StartDate, m.EndDate)))
            errors.Add(new(row.Line, "Membership", "Thời gian tham gia lớp bị trùng với lịch sử đã ghi nhận của học sinh. Hãy kiểm tra ngày vào lớp và ngày kết thúc trong tệp. Hệ thống không tự kết thúc lịch sử cũ hoặc chuyển lớp; chưa nhập dòng nào."));
        return errors;
    }
    private static async Task<(IReadOnlyList<ImportError> Errors, List<RosterStudentPreview> Students)> ValidateAsync(
        HarmoniaDbContext db, ParsedRoster csv, CancellationToken ct)
    {
        if (csv.Errors.Count > 0) return (csv.Errors, new());
        var codes = csv.Rows.Select(r => r.StudentCode).ToArray();
        var existing = await db.Students.AsNoTracking().Where(s => codes.Contains(s.StudentCode)).ToListAsync(ct);
        var ids = existing.Select(s => s.StudentId).ToArray();
        var memberships = await db.ClassMemberships.AsNoTracking().Where(m => ids.Contains(m.StudentId)).ToListAsync(ct);
        var lookup = existing.ToDictionary(s => s.StudentCode, StringComparer.Ordinal);
        var errors = new List<ImportError>();
        var students = new List<RosterStudentPreview>();
        foreach (var row in csv.Rows)
        {
            lookup.TryGetValue(row.StudentCode, out var student);
            if (student is not null) errors.AddRange(MatchExisting(row, student, memberships.Where(m => m.StudentId == student.StudentId)));
            students.Add(new(row.StudentCode, student?.FullName ?? row.FullName, row.DateOfBirth, row.Gender,
                row.StartDate, row.EndDate, student?.StudentId, student?.Status ?? "Active"));
        }
        return (errors.Take(100).ToArray(), students);
    }
    public async Task<RosterPreparation?> PrepareAsync(long classId, byte[] bytes, string fileName, string owner, CancellationToken ct = default)
    {
        var page = await GetPageAsync(classId, ct);
        if (page is null) return null;
        if (!page.CanImport) return new(page, null);
        var csv = parser.Parse(bytes);
        await using var db = await factory.CreateDbContextAsync(ct);
        var validation = await ValidateAsync(db, csv, ct);
        if (validation.Errors.Count > 0) return new(page with { Errors = validation.Errors }, null);
        var safeName = Path.GetFileName(fileName.Replace('\\', '/'));
        safeName = safeName[..Math.Min(safeName.Length, 200)];
        var preview = new RosterPreviewViewModel(page.Class, safeName, RosterPreviewStore.NewToken(),
            DateTimeOffset.UtcNow.AddMinutes(10), validation.Students);
        if (!store.Add(new(owner, preview, csv))) return new(page with { Errors = new[] { new ImportError(null, "Preview", "Bộ nhớ preview đầy. Vui lòng thử lại sau.") } }, null);
        return new(page, preview);
    }
    public RosterPreviewViewModel? GetPreview(long classId, string token, string owner) => store.Get(classId, token, owner)?.Preview;
    public async Task<RosterImportOutcome?> ConfirmAsync(long classId, string token, string owner, CancellationToken ct = default)
    {
        var ticket = store.Get(classId, token, owner);
        if (ticket is null || !ticket.TryUse()) return null;
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var commitStarted = false;
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Class\" WHERE \"Class_ID\" = {classId} FOR UPDATE", ct);
            var header = await AcademicManagementService.ReadHeaderAsync(db, classId, ct);
            if (header != ticket.Preview.Class || header is null || !Allowed(header))
                return await RejectAsync("Class", "Lớp đã thay đổi hoặc không còn được phép nhập. Tải file lên lại.");
            // Lock existing students in a stable order across classes; UNIQUE and EXCLUDE also guard concurrent writers.
            var codes = ticket.Csv.Rows.Select(r => r.StudentCode).Order(StringComparer.Ordinal).ToArray();
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Student\" WHERE \"StudentCode\" = ANY({codes}) ORDER BY \"Student_ID\" FOR UPDATE", ct);
            var validation = await ValidateAsync(db, ticket.Csv, ct);
            if (validation.Errors.Count > 0) { await tx.RollbackAsync(ct); return Failure(validation.Errors); }
            // Do not silently change a previewed create into reuse (or reuse into create) after a concurrent change.
            if (!validation.Students.SequenceEqual(ticket.Preview.Students))
                return await RejectAsync("Preview", "Hồ sơ học sinh đã thay đổi sau preview. Tải lại để kiểm tra trước khi nhập.");
            var now = DateTime.UtcNow;
            foreach (var row in ticket.Csv.Rows)
            {
                var preview = validation.Students.Single(s => s.StudentCode == row.StudentCode);
                var membership = new ClassMembership { ClassId = classId, StartDate = row.StartDate, EndDate = row.EndDate,
                    CreatedAt = now, CreatedByUserId = actor!.RequiredId };
                if (preview.ExistingStudentId is long studentId) membership.StudentId = studentId;
                else membership.Student = new Student { StudentCode = row.StudentCode, FullName = row.FullName,
                    DateOfBirth = row.DateOfBirth, Gender = row.Gender, Status = "Active", CreatedAt = now,
                    CreatedByUserId = actor!.RequiredId };
                db.ClassMemberships.Add(membership);
            }
            await db.SaveChangesAsync(ct);
            commitStarted = true;
            await tx.CommitAsync(ct);
            return new(header, true, ticket.Preview.NewCount, ticket.Preview.ExistingCount, ticket.Csv.Rows.Count, Array.Empty<ImportError>());
        }
        catch (Exception ex) when (ex is DbUpdateException or NpgsqlException or OperationCanceledException)
        {
            logger.LogWarning("Roster import did not complete normally ({FailureType}).", ex.GetType().Name);
            try { await tx.RollbackAsync(CancellationToken.None); } catch (Exception rollback) when (rollback is NpgsqlException or InvalidOperationException) { }
            // Without a roster ImportBatch table, do not claim rollback if a commit acknowledgement was lost.
            if (commitStarted) return new(ticket.Preview.Class, false, 0, 0, 0,
                new[] { new ImportError(null, "Commit", "Chưa xác nhận được kết quả lưu. Kiểm tra danh sách lớp trước khi nhập lại.") }, true);
            return Failure(new[] { new ImportError(null, "Import", "Không lưu được toàn bộ danh sách; đã rollback. Có thể dữ liệu hoặc membership vừa được cập nhật ở phiên khác. Kiểm tra và tải lại file.") });
        }
        RosterImportOutcome Failure(IReadOnlyList<ImportError> errors) => new(ticket.Preview.Class, false, 0, 0, 0, errors);
        async Task<RosterImportOutcome> RejectAsync(string field, string message)
        { await tx.RollbackAsync(ct); return Failure(new[] { new ImportError(null, field, message) }); }
    }
}
