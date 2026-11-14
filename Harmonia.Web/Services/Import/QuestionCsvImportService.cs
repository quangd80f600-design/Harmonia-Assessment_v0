using System.Data;
using Harmonia.Web.Data;
using Harmonia.Web.Models.Entities;
using Harmonia.Web.Services.Security;
using Harmonia.Web.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Harmonia.Web.Services.Import;

public sealed class QuestionCsvImportService(IDbContextFactory<HarmoniaDbContext> factory, QuestionCsvParser parser,
    QuestionCsvPreviewStore store, IOptions<AuthoringOptions> options, CurrentActor actor)
{
    public bool CanImport => options.Value.Enabled && actor.Id is not null && (actor.IsManager || actor.TeacherId.HasValue);
    private static IQueryable<QuestionCsvSkill> Skills(HarmoniaDbContext db, string[]? codes = null) => db.Subskills.AsNoTracking()
        .Where(s => s.Status == "Active" && s.KnowledgeArea.Status == "Active" && (codes == null || codes.Contains(s.SubskillCode)))
        .OrderBy(s => s.SubskillCode)
        .Select(s => new QuestionCsvSkill(s.SubskillId, s.SubskillCode, s.SubskillName, s.KnowledgeAreaId,
            s.KnowledgeArea.KnowledgeAreaName, s.KnowledgeArea.CurriculumUnitId,
            s.KnowledgeArea.CurriculumUnit.UnitName, s.KnowledgeArea.CurriculumUnit.Grade));

    public async Task<QuestionCsvPage> GetPageAsync(CancellationToken ct = default)
    {
        if (!CanImport) return new([], [new(null, "Access", "Chức năng chưa bật hoặc không có quyền tạo câu hỏi.")]);
        await using var db = await factory.CreateDbContextAsync(ct);
        return new(await Skills(db).ToListAsync(ct), []);
    }

    public async Task<QuestionCsvPreparation> PrepareAsync(byte[] bytes, string owner, CancellationToken ct = default, string usagePurpose = "ForTest")
    {
        if (!CanImport) return new(null, [new(null, "Access", "Chức năng chưa bật hoặc không có quyền tạo câu hỏi.")]);
        if (!Harmonia.Web.Models.QuestionPurposes.IsValid(usagePurpose))
            return new(null, [new(null, "UsagePurpose", "Chọn mục đích Đánh giá hoặc Luyện tập can thiệp cho toàn file.")]);
        var csv = parser.Parse(bytes);
        if (csv.Errors.Count > 0) return new(null, csv.Errors);
        await using var db = await factory.CreateDbContextAsync(ct);
        var (rows, errors) = await ValidateAsync(db, csv.Rows, ct);
        if (errors.Count > 0) return new(null, errors);
        var preview = new QuestionCsvPreview(QuestionCsvPreviewStore.NewToken(), DateTimeOffset.UtcNow.AddMinutes(15), rows.AsReadOnly()) { UsagePurpose = usagePurpose };
        return store.Add(new(owner, preview)) ? new(preview, [])
            : new(null, [new(null, "Preview", "Bộ nhớ xem trước đang đầy. Hãy thử lại sau.")]);
    }

    public QuestionCsvPreview? GetPreview(string token, string owner) => CanImport ? store.Get(token, owner)?.Preview : null;

    private static async Task<(List<QuestionCsvPreviewRow> Rows, List<ImportError> Errors)> ValidateAsync(
        HarmoniaDbContext db, IReadOnlyList<QuestionCsvRow> input, CancellationToken ct)
    {
        var skillCodes = input.Select(r => r.SubskillCode).Distinct().ToArray();
        var codes = input.Select(r => r.QuestionCode).ToArray();
        var skills = await Skills(db, skillCodes).ToDictionaryAsync(s => s.Code, ct);
        var existing = (await db.Questions.AsNoTracking().Where(q => codes.Contains(q.QuestionCode))
            .Select(q => q.QuestionCode).ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);
        var errors = new List<ImportError>();
        var rows = new List<QuestionCsvPreviewRow>();
        foreach (var row in input)
        {
            if (existing.Contains(row.QuestionCode)) errors.Add(new(row.Line, "QuestionCode", "Mã câu hỏi đã tồn tại; không ghi đè."));
            if (!skills.TryGetValue(row.SubskillCode, out var skill))
                errors.Add(new(row.Line, "SubskillCode", "Không tìm thấy kỹ năng hoặc kỹ năng/nhóm kiến thức đã ngừng hoạt động."));
            else rows.Add(new(row, skill));
        }
        return (rows, errors);
    }

    public async Task<QuestionCsvOutcome?> ConfirmAsync(string token, string owner, CancellationToken ct = default)
    {
        if (!CanImport) return new(false, 0, "Chức năng chưa bật hoặc không có quyền tạo câu hỏi.");
        var ticket = store.Take(token, owner);
        if (ticket is null) return null;
        if (!Harmonia.Web.Models.QuestionPurposes.IsValid(ticket.Preview.UsagePurpose))
            return new(false, 0, "Mục đích sử dụng trong xem trước không hợp lệ. Hãy tải lại file.");
        var commitStarted = false;
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            var ids = ticket.Preview.Rows.Select(r => r.Skill.Id).Distinct().Order().ToArray();
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT s.\"Subskill_ID\" FROM \"Subskill\" s JOIN \"KnowledgeArea\" k ON k.\"KnowledgeArea_ID\"=s.\"KnowledgeArea_ID\" JOIN \"CurriculumUnit\" u ON u.\"CurriculumUnit_ID\"=k.\"CurriculumUnit_ID\" WHERE s.\"Subskill_ID\"=ANY({ids}) ORDER BY s.\"Subskill_ID\" FOR SHARE OF s,k,u", ct);
            var (rows, errors) = await ValidateAsync(db, ticket.Preview.Rows.Select(r => r.Question).ToArray(), ct);
            if (errors.Count > 0 || !rows.SequenceEqual(ticket.Preview.Rows))
                return new(false, 0, "Mã câu hỏi hoặc cây kiến thức đã thay đổi sau xem trước. Không nhập câu nào; tải lại file để kiểm tra.");
            // The ticket may expire while waiting for another taxonomy writer's lock.
            if (DateTimeOffset.UtcNow >= ticket.Preview.ExpiresAt)
                return new(false, 0, "Xem trước đã hết hạn trong khi chờ lưu. Không nhập câu nào; tải lại file.");
            foreach (var row in rows)
            {
                var q = row.Question;
                db.Questions.Add(new Question { QuestionCode = q.QuestionCode, SubskillId = row.Skill.Id,
                    QuestionText = q.QuestionText, OptionA = q.OptionA, OptionB = q.OptionB,
                    OptionC = q.OptionC, OptionD = q.OptionD, AnswerKey = q.AnswerKey,
                    DifficultyLevel = 1, Explanation = "", Status = "Active", UsagePurpose = ticket.Preview.UsagePurpose });
            }
            // UNIQUE remains authoritative if a concurrent request inserts the same code after validation.
            await db.SaveChangesAsync(ct);
            commitStarted = true;
            await tx.CommitAsync(ct);
            return new(true, rows.Count, $"Đã thêm {rows.Count} câu hỏi mới vào ngân hàng dùng chung.");
        }
        catch (Exception ex) when (ex is DbUpdateException or NpgsqlException or OperationCanceledException)
        {
            // Disposal rolls back a transaction that did not commit. Do not log CSV content or connection details.
            return commitStarted
                ? new(false, 0, "Chưa xác nhận được kết quả lưu. Kiểm tra mã câu hỏi trong ngân hàng trước khi nhập lại.", true)
                : new(false, 0, "Không lưu được toàn bộ file; giao dịch đã bị hủy. Dữ liệu có thể vừa thay đổi. Kiểm tra ngân hàng và tải lại file.");
        }
    }
}
