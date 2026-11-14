using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Harmonia.Web.Data;
using Harmonia.Web.Models.Entities;
using Harmonia.Web.Services.Security;
using Harmonia.Web.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Harmonia.Web.Services;

public sealed class AssessmentFollowupPlanService(IDbContextFactory<HarmoniaDbContext> factory,
    BusinessAccess access, CurrentActor actor, IOptions<AuthoringOptions> options)
{
    private static string Version(Assessment row) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        JsonSerializer.Serialize(new { row.AssessmentId, row.AssessmentDate, row.Status, row.PlannedFollowupDate, row.UpdatedAt }))));

    public async Task<AssessmentFollowupPlanPage?> GetAsync(long id, CancellationToken ct = default)
    {
        if (id <= 0 || actor.Id is null) return null;
        await using var db = await factory.CreateDbContextAsync(ct);
        var row = await db.Assessments.AsNoTracking().Where(access.AssessmentScope()).Where(a => a.AssessmentId == id)
            .Select(a => new { Row = new Assessment { AssessmentId = a.AssessmentId, AssessmentDate = a.AssessmentDate,
                    PlannedFollowupDate = a.PlannedFollowupDate, UpdatedAt = a.UpdatedAt, Status = a.Status },
                a.AssessmentName, a.Class.ClassName }).SingleOrDefaultAsync(ct);
        if (row is null) return null;
        var edit = options.Value.Enabled && (row.Row.Status is "Open" or "Completed")
            && await db.Assessments.Where(access.AssessmentWriteScope()).AnyAsync(a => a.AssessmentId == id, ct);
        var summary = new AssessmentFollowupPlanSummary(id, row.AssessmentName, row.ClassName, row.Row.AssessmentDate,
            row.Row.PlannedFollowupDate, edit);
        return new(summary, new() { PlannedFollowupDate = summary.PlannedFollowupDate,
            ExpectedVersion = edit ? Version(row.Row) : "" });
    }

    public async Task<AssessmentFollowupPlanResult> SaveAsync(long id, AssessmentFollowupPlanInput input, CancellationToken ct = default)
    {
        if (id <= 0 || actor.Id is null || !options.Value.Enabled)
            return new(false, "Không có quyền chỉnh sửa ngày dự kiến đánh giá lại.", NotFound: true);
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(input, new ValidationContext(input), errors, true))
            return new(false, errors.First().ErrorMessage ?? "Biểu mẫu chưa hợp lệ.");
        if (input.Operation is not ("Save" or "Clear")) return new(false, "Thao tác chưa hợp lệ. Hãy mở lại biểu mẫu.");
        if (input.Operation == "Clear" && !input.ConfirmClear) return new(false, "Hãy xác nhận chỉ xóa lời nhắc.");
        if (input.Operation == "Save" && input.PlannedFollowupDate is null)
            return new(false, "Chọn ngày dự kiến. Nếu muốn gỡ lịch, hãy dùng Xóa ngày dự kiến.");

        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var commitStarted = false;
        try
        {
            // Scope the lock itself; never lock an out-of-scope row because an ID was supplied.
            var candidate = await db.Assessments.Where(access.AssessmentWriteScope()).AnyAsync(a => a.AssessmentId == id, ct);
            if (!candidate) return new(false, "Dữ liệu không tồn tại hoặc nằm ngoài phạm vi quyền.", NotFound: true);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM public.\"Assessment\" WHERE \"Assessment_ID\"={id} FOR UPDATE", ct);
            var row = await db.Assessments.Where(access.AssessmentWriteScope()).SingleOrDefaultAsync(a => a.AssessmentId == id, ct);
            if (row is null) return new(false, "Dữ liệu không tồn tại hoặc nằm ngoài phạm vi quyền.", NotFound: true);
            if (row.Status is not ("Open" or "Completed"))
                return new(false, "Chỉ chỉnh sửa ngày dự kiến của đợt đang mở hoặc đã hoàn tất.");
            if (!string.Equals(input.ExpectedVersion, Version(row), StringComparison.Ordinal))
                return new(false, "Ngày dự kiến hoặc thông tin đợt đánh giá đã thay đổi. Hãy mở lại biểu mẫu trước khi lưu.", Conflict: true);
            var planned = input.Operation == "Clear" ? null : input.PlannedFollowupDate;
            if (AssessmentFollowupPlanRules.ValidateDate(row.AssessmentDate, planned) is string error) return new(false, error);
            var message = planned.HasValue ? "Đã lưu ngày dự kiến đánh giá lại." : "Đã xóa ngày dự kiến đánh giá lại.";
            // A no-op does not touch UpdatedAt or invalidate another unchanged form.
            if (row.PlannedFollowupDate == planned) return new(true, message);
            row.PlannedFollowupDate = planned;
            row.UpdatedAt = DateTime.UtcNow;
            row.UpdatedByUserId = actor.RequiredId;
            await db.SaveChangesAsync(ct);
            commitStarted = true;
            await tx.CommitAsync(ct);
            return new(true, message);
        }
        catch (Exception ex) when (ex is NpgsqlException or DbUpdateException or OperationCanceledException)
        {
            try { await tx.RollbackAsync(CancellationToken.None); }
            catch (Exception rollback) when (rollback is NpgsqlException or InvalidOperationException) { }
            return new(false, commitStarted
                ? "Chưa xác nhận được kết quả lưu. Hãy kiểm tra ngày dự kiến trước khi thử lại."
                : "Chưa lưu được ngày dự kiến. Thay đổi chưa được xác nhận; hãy mở lại biểu mẫu và kiểm tra.", Uncertain: commitStarted);
        }
    }
}
