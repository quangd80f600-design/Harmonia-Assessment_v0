using Harmonia.Web.Services.Security;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Harmonia.Web.Data;
using Harmonia.Web.Models.Entities;
using Harmonia.Web.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Harmonia.Web.Services.Import;

internal sealed record ImportQuestion(long Id, short Number, string Key, long SubskillId,
    string Text, string A, string B, string C, string D);
internal sealed record ImportContext(AssessmentHeader Header, long ClassId, long TestId,
    string Status, string TestStatus, IReadOnlyList<ImportQuestion> Questions)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string Fingerprint => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(this))));
}
internal sealed record ImportCandidate(long Id, string Code, string Name, bool IsMember, bool HasAttempt);
internal sealed record GradedStudent(long Id, string Code, string Name, bool[] Correct, int Blank)
{
    public decimal Score => Math.Round(10m * Correct.Count(c => c) / Correct.Length, 2, MidpointRounding.AwayFromZero);
    public ImportStudentPreview Preview => new(Code, Name, Correct.Length, Correct.Count(c => c),
        Correct.Count(c => !c), Blank, Score);
}
internal sealed class ImportValidationException(IReadOnlyList<ImportError> errors) : Exception
{
    public IReadOnlyList<ImportError> Errors { get; } = errors;
}

public sealed class AssessmentImportService(IDbContextFactory<HarmoniaDbContext> factory,
    CsvResultParser parser, ImportPreviewStore store, IOptions<CsvImportOptions> options,
    ILogger<AssessmentImportService> logger, CurrentActor? actor = null, BusinessAccess? access = null)
{
    private readonly CsvImportOptions _options = options.Value;

    private string? BlockReason(ImportContext context)
    {
        if (!_options.Enabled) return "Chức năng nhập CSV chưa được bật cho môi trường này.";
        if (string.IsNullOrWhiteSpace(actor?.Id))
            return "Chưa có tài khoản xác thực để ghi kết quả.";
        if (context.Status is not ("Open" or "Completed")) return "Chỉ nhập cho Assessment Open hoặc nhập bù ở Completed.";
        if (context.TestStatus != "Published") return "Đề phải được Published trước khi nhập kết quả.";
        if (context.Questions.Count is 0 or > CsvResultParser.MaxQuestions) return "Đề cần có từ 1 đến 200 câu hỏi snapshot.";
        if (context.Questions.Any(q => q.Key is not ("A" or "B" or "C" or "D"))
            || context.Questions.Select(q => q.Number).Distinct().Count() != context.Questions.Count)
            return "Snapshot của đề không hợp lệ; chưa thể nhập kết quả.";
        return null;
    }
    private ImportPageViewModel Page(ImportContext c, ResultCompletion? completion,
        IReadOnlyList<ImportError>? errors = null) =>
        new(c.Header, c.Questions.Count, actor!.RequiredId,
            BlockReason(c) ?? (completion is null ? "Chưa xác định được tiến độ kết quả. Vui lòng mở lại Assessment."
                : !completion.HasStudents ? "Không có học sinh thuộc lớp tại ngày đánh giá để nhập kết quả."
                : completion.IsComplete ? "Đã nhập đủ kết quả; không còn học sinh cần nhập bổ sung."
                : null), errors ?? Array.Empty<ImportError>()) { ResultCompletion = completion };

    private static Task<ResultCompletion?> CompletionAsync(HarmoniaDbContext db, long id, CancellationToken ct) =>
        new AssessmentParticipationService(db).GetCompletionAsync(id, ct);

    private static async Task<ImportContext?> ReadContextAsync(HarmoniaDbContext db, long id, CancellationToken ct)
    {
        var a = await db.Assessments.AsNoTracking().Where(a => a.AssessmentId == id)
            .Select(a => new { a.ClassId, a.TestId, a.Status, TestStatus = a.Test.Status,
                Header = new AssessmentHeader(a.AssessmentId, a.AssessmentName, a.AssessmentDate, a.Class.ClassName, a.Test.TestName) })
            .SingleOrDefaultAsync(ct);
        if (a is null) return null;
        var questions = await db.QuestionInTests.AsNoTracking().Where(q => q.TestId == a.TestId)
            .OrderBy(q => q.QuestionNumber).Select(q => new ImportQuestion(q.QuestionInTestId, q.QuestionNumber,
                q.SnSanswerKey, q.SnSsubskillId, q.SnSquestionText, q.SnSoptionA, q.SnSoptionB, q.SnSoptionC, q.SnSoptionD)).ToListAsync(ct);
        return new(a.Header, a.ClassId, a.TestId, a.Status, a.TestStatus, questions);
    }
    public async Task<ImportPageViewModel?> GetPageAsync(long id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        if (access == null || !await access.AssessmentAsync(id, false, ct)) return null;
        var c = await ReadContextAsync(db, id, ct);
        if (c is null) return null;
        var page = Page(c, await CompletionAsync(db, id, ct));
        return await access.AssessmentAsync(id, true, ct) ? page : page with { BlockReason = "Bạn có quyền xem lịch sử; nhập kết quả cần phân công tại ngày đánh giá. Liên hệ quản lý." };
    }
    public async Task<CsvTemplateDownload?> GetTemplateAsync(long id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        if (access == null || !await access.AssessmentAsync(id, false, ct)) return null;
        var c = await ReadContextAsync(db, id, ct);
        if (c is null || !Page(c, await CompletionAsync(db, id, ct)).CanImport) return null;
        var header = "StudentCode," + string.Join(',', c.Questions.Select(q => $"Q{q.Number}")) + "\r\n";
        return new(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(header)).ToArray(), TemplateFileName(c.Header));
    }
    internal static string TemplateFileName(AssessmentHeader header)
    {
        static string Clean(string value, string fallback)
        {
            var cleaned = new string(value.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '-').ToArray());
            while (cleaned.Contains("--", StringComparison.Ordinal)) cleaned = cleaned.Replace("--", "-");
            cleaned = cleaned.Trim('-', '.', '_');
            return cleaned.Length == 0 ? fallback : cleaned[..Math.Min(cleaned.Length, 60)];
        }
        return $"Harmonia_{Clean(header.ClassName, "Lop")}_{Clean(header.AssessmentName, "Dot-danh-gia")}_{header.AssessmentDate:yyyy-MM-dd}.csv";
    }

    private static async Task<List<ImportCandidate>> CandidatesAsync(HarmoniaDbContext db, ImportContext c, ParsedCsv csv, CancellationToken ct)
    {
        var codes = csv.Rows.Select(r => r.StudentCode).Distinct().ToArray();
        return await db.Students.AsNoTracking().Where(s => codes.Contains(s.StudentCode))
            .Select(s => new ImportCandidate(s.StudentId, s.StudentCode, s.FullName,
                s.ClassMemberships.Any(m => m.ClassId == c.ClassId && m.StartDate <= c.Header.AssessmentDate
                    && (m.EndDate == null || m.EndDate > c.Header.AssessmentDate)),
                s.Attempts.Any(a => a.AssessmentId == c.Header.AssessmentId))).ToListAsync(ct);
    }
    internal static (IReadOnlyList<ImportError> Errors, IReadOnlyList<GradedStudent> Students) ValidateAndGrade(
        ImportContext context, ParsedCsv csv, IEnumerable<ImportCandidate> candidates)
    {
        var errors = csv.Errors.ToList();
        var graded = new List<GradedStudent>();
        if (errors.Count > 0) return (errors, graded);
        if (!csv.QuestionNumbers.Order().SequenceEqual(context.Questions.Select(q => q.Number).Order()))
            return (new[] { new ImportError(1, "Header", "Các cột câu hỏi không khớp với đề của đợt đánh giá này. Hãy tải lại mẫu CSV từ đúng đợt đang nhập và giữ nguyên dòng tên cột.") }, graded);
        var positions = csv.QuestionNumbers.Select((n, i) => (n, i)).ToDictionary(p => p.n, p => p.i);
        var lookup = candidates.ToDictionary(c => c.Code, StringComparer.Ordinal);
        foreach (var row in csv.Rows)
        {
            if (!lookup.TryGetValue(row.StudentCode, out var candidate))
                errors.Add(new(row.Line, "StudentCode", "Không tìm thấy mã học sinh."));
            else if (!candidate.IsMember) errors.Add(new(row.Line, "StudentCode", "Học sinh không thuộc lớp tại ngày đánh giá."));
            else if (candidate.HasAttempt) errors.Add(new(row.Line, "StudentCode", "Học sinh đã có kết quả trong đợt này. Hãy bỏ dòng của học sinh này khỏi tệp và xem trước lại. Hệ thống không ghi đè hoặc tự bỏ qua dòng lỗi."));
            else
            {
                var answers = context.Questions.Select(q => row.Answers[positions[q.Number]]).ToArray();
                // Explicit empty CSV cells are graded incorrect; missing columns were rejected by the parser.
                graded.Add(new(candidate.Id, candidate.Code, candidate.Name,
                    context.Questions.Select((q, i) => answers[i] == q.Key).ToArray(), answers.Count(a => a == "")));
            }
            if (errors.Count >= 100) break;
        }
        return (errors.Take(100).ToArray(), errors.Count == 0 ? graded : Array.Empty<GradedStudent>());
    }
    public async Task<ImportPreparation?> PrepareAsync(long id, byte[] bytes, string fileName, string owner, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        if (access == null || !await access.AssessmentAsync(id, true, ct)) return null;
        var c = await ReadContextAsync(db, id, ct);
        if (c is null) return null;
        var completion = await CompletionAsync(db, id, ct);
        var page = Page(c, completion);
        if (!page.CanImport) return new(page, null);
        var csv = parser.Parse(bytes);
        var candidates = csv.Errors.Count == 0 ? await CandidatesAsync(db, c, csv, ct) : new List<ImportCandidate>();
        var validation = ValidateAndGrade(c, csv, candidates);
        if (validation.Errors.Count > 0) return new(Page(c, completion, validation.Errors), null);
        var safeName = Path.GetFileName(fileName.Replace('\\', '/'));
        if (safeName.Length > 200) safeName = safeName[..200];
        if (safeName.Length == 0) safeName = "results.csv";
        var token = ImportPreviewStore.NewToken();
        var preview = new ImportPreviewViewModel(page, safeName, token, DateTimeOffset.UtcNow.AddMinutes(10),
            validation.Students.Select(s => s.Preview).ToArray());
        var ticket = new ImportTicket(owner, id, c.Fingerprint, safeName, csv, preview);
        if (!store.Add(token, ticket)) return new(Page(c, completion, new[] { new ImportError(null, "Preview", "Bộ nhớ preview đang đầy. Vui lòng thử lại sau.") }), null);
        return new(page, preview);
    }
    public ImportPreviewViewModel? GetPreview(long id, string token, string owner) => store.Get(token, owner, id)?.Preview;

    internal static ImportBatch BuildBatch(ImportContext c, IReadOnlyList<GradedStudent> students, string fileName, string actor)
    {
        var batch = new ImportBatch { AssessmentId = c.Header.AssessmentId, OriginalFileName = fileName,
            ImportedByUserId = actor, Status = "Pending", TotalRows = students.Count, CreatedAt = DateTime.UtcNow };
        foreach (var student in students)
        {
            var attempt = new Attempt { AssessmentId = c.Header.AssessmentId, StudentId = student.Id, Score = student.Score };
            for (var i = 0; i < c.Questions.Count; i++)
                attempt.Responses.Add(new Response { QuestionInTestId = c.Questions[i].Id, IsCorrect = student.Correct[i] });
            batch.Attempts.Add(attempt);
        }
        return batch;
    }
    public async Task<ImportOutcome?> ConfirmAsync(long id, string token, string owner, CancellationToken ct = default)
    {
        if (access == null || !await access.AssessmentAsync(id, true, ct)) return null;
        var ticket = store.Get(token, owner, id);
        if (ticket is null) return null;
        await ticket.Gate.WaitAsync(ct);
        try
        {
            // Re-check expiry after waiting, then claim the server-validated payload once.
            // A concurrent/replayed request must be rejected, not returned a cached success.
            if (!ticket.TryUse(DateTimeOffset.UtcNow)) return null;
            return await PersistAsync(ticket, ct);
        }
        finally { ticket.Gate.Release(); }
    }
    private async Task<ImportOutcome> PersistAsync(ImportTicket ticket, CancellationToken ct)
    {
        var header = ticket.Preview.Page.Assessment;
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var commitStarted = false;
        ImportBatch? batch = null;
        IReadOnlyList<ImportError> errors;
        try
        {
            // Serialize imports for this Assessment; business UNIQUE keys also guard other writers.
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Assessment\" WHERE \"Assessment_ID\" = {ticket.AssessmentId} FOR UPDATE", ct);
            if (access == null || !await access.AssessmentAsync(ticket.AssessmentId, true, ct)) throw new ImportValidationException(new[] { new ImportError(null, "Assessment", "Không còn quyền ghi kết quả.") });
            var current = await ReadContextAsync(db, ticket.AssessmentId, ct);
            if (current is null) throw new ImportValidationException(new[] { new ImportError(null, "Assessment", "Assessment không còn tồn tại.") });
            var block = Page(current, await CompletionAsync(db, ticket.AssessmentId, ct)).BlockReason;
            if (block is not null) throw new ImportValidationException(new[] { new ImportError(null, "Assessment", block) });
            if (current.Fingerprint != ticket.Fingerprint)
                throw new ImportValidationException(new[] { new ImportError(null, "Snapshot", "Assessment hoặc snapshot đã thay đổi. Upload lại để kiểm tra.") });
            var validation = ValidateAndGrade(current, ticket.Csv, await CandidatesAsync(db, current, ticket.Csv, ct));
            if (validation.Errors.Count > 0) throw new ImportValidationException(validation.Errors);
            batch = BuildBatch(current, validation.Students, ticket.FileName, actor!.RequiredId);
            db.ImportBatches.Add(batch);
            await db.SaveChangesAsync(ct);
            batch.Status = "Succeeded";
            batch.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            commitStarted = true;
            await tx.CommitAsync(ct);
            return new(header, true, batch.ImportBatchId, batch.TotalRows, batch.Attempts.Sum(a => a.Responses.Count), Array.Empty<ImportError>());
        }
        catch (Exception ex) when (ex is ImportValidationException or DbUpdateException or NpgsqlException or OperationCanceledException)
        {
            logger.LogWarning("CSV import did not complete normally ({FailureType}).", ex.GetType().Name);
            try { await tx.RollbackAsync(CancellationToken.None); } catch (Exception rollbackError) when (rollbackError is NpgsqlException or InvalidOperationException) { }
            if (commitStarted)
            {
                // A lost commit acknowledgement is not proof of failure. Never retry/write a Failed audit blindly.
                try
                {
                    await using var verify = await factory.CreateDbContextAsync(CancellationToken.None);
                    var saved = await verify.ImportBatches.AsNoTracking().SingleOrDefaultAsync(b => b.ImportBatchId == batch!.ImportBatchId);
                    if (saved?.Status == "Succeeded") return new(header, true, saved.ImportBatchId, saved.TotalRows,
                        batch!.Attempts.Sum(a => a.Responses.Count), Array.Empty<ImportError>());
                }
                catch (Exception verificationError) when (verificationError is NpgsqlException or InvalidOperationException) { }
                return new(header, false, null, 0, 0, new[] { new ImportError(null, "Commit", "Chưa xác nhận được kết quả lưu. Hãy kiểm tra danh sách học sinh và kết quả trong Tổng quan đợt đánh giá trước khi nhập lại. Không gửi lại lần xác nhận này.") }, true);
            }
            errors = ex is ImportValidationException validationError ? validationError.Errors
                : new[] { new ImportError(null, "Import", "Không lưu được toàn bộ kết quả. Transaction đã rollback; không có kết quả nhập một phần.") };
        }
        long? failedId = null;
        try
        {
            await using var audit = await factory.CreateDbContextAsync(CancellationToken.None);
            var failed = new ImportBatch { AssessmentId = ticket.AssessmentId, OriginalFileName = ticket.FileName,
                ImportedByUserId = actor!.RequiredId, Status = "Failed", TotalRows = ticket.Csv.Rows.Count,
                ErrorSummary = string.Join("; ", errors.Select(e => $"Dòng {e.Line}: {e.Field}: {e.Message}")),
                CreatedAt = DateTime.UtcNow, CompletedAt = DateTime.UtcNow };
            audit.ImportBatches.Add(failed);
            await audit.SaveChangesAsync(CancellationToken.None);
            failedId = failed.ImportBatchId;
        }
        catch (Exception auditError) when (auditError is DbUpdateException or NpgsqlException)
        { logger.LogWarning("Could not persist failed CSV audit ({FailureType}).", auditError.GetType().Name); }
        return new(header, false, failedId, 0, 0, errors);
    }
}
