using Harmonia.Web.Services.Security;
using System.ComponentModel.DataAnnotations;
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

namespace Harmonia.Web.Services;

public sealed class AuthoringService(IDbContextFactory<HarmoniaDbContext> factory, IOptions<AuthoringOptions> options, CurrentActor? actor = null, BusinessAccess? access = null)
{
    public bool CanWrite => options.Value.CanWrite;
    internal static string? Validate(object input)
    {
        var errors = new List<ValidationResult>();
        Validator.TryValidateObject(input, new ValidationContext(input), errors, true);
        return errors.Count == 0 ? null : string.Join(" ", errors.Select(e => e.ErrorMessage));
    }
    internal static string? ValidateQuestion(QuestionInput input)
    {
        input.QuestionCode = input.QuestionCode?.Trim() ?? ""; input.QuestionText = input.QuestionText?.Trim() ?? "";
        input.OptionA = input.OptionA?.Trim() ?? ""; input.OptionB = input.OptionB?.Trim() ?? "";
        input.OptionC = input.OptionC?.Trim() ?? ""; input.OptionD = input.OptionD?.Trim() ?? "";
        input.AnswerKey = input.AnswerKey?.Trim().ToUpperInvariant() ?? ""; input.Explanation = input.Explanation?.Trim() ?? "";
        return Validate(input) ?? (input.QuestionCode.Any(char.IsControl) ? "Mã câu hỏi không được chứa ký tự điều khiển." : null)
            ?? (new[] { input.OptionA, input.OptionB, input.OptionC, input.OptionD }.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 4
                ? "Bốn lựa chọn A–D phải khác nhau." : null);
    }
    internal static string? ValidatePicks(TestInput input)
    {
        var error = Validate(input); if (error is not null) return error;
        if (input.Picks.Any(p => p is null || Validate(p) is not null)) return "Có dòng chọn câu hỏi không hợp lệ.";
        var selected = input.Picks.Where(p => p.Number.HasValue).ToArray();
        if (selected.Length is 0 or > 200) return "Chọn từ 1 đến 200 câu hỏi.";
        if (selected.Select(p => p.QuestionId).Distinct().Count() != selected.Length) return "Không chọn trùng câu hỏi.";
        if (!selected.Select(p => (int)p.Number!.Value).Order().SequenceEqual(Enumerable.Range(1, selected.Length)))
            return "Thứ tự câu hỏi chưa hợp lệ. Hãy sắp xếp lại danh sách đã chọn rồi lưu; số thứ tự phải liên tiếp từ 1 và không trùng.";
        return null;
    }
    internal static string? ValidateSnapshots(IReadOnlyList<TestSnapshotRow> rows)
    {
        if (rows.Count is 0 or > 200 || rows.Select(q => q.QuestionId).Distinct().Count() != rows.Count
            || !rows.Select(q => (int)q.Number).Order().SequenceEqual(Enumerable.Range(1, rows.Count)))
            return "Đề cần 1–200 câu snapshot, không trùng câu và đánh số liên tiếp từ 1.";
        if (rows.Any(q => q.Key is not ("A" or "B" or "C" or "D") || q.SubskillId <= 0
            || new[] { q.Text, q.A, q.B, q.C, q.D }.Any(string.IsNullOrWhiteSpace))) return "Nội dung hoặc đáp án snapshot chưa hợp lệ.";
        return null;
    }

    public async Task<TaxonomyPage?> GetTaxonomyAsync(TaxonomyFilter filter, CancellationToken ct = default)
    {
        if (Validate(filter) is not null) return null;
        await using var db = await factory.CreateDbContextAsync(ct);
        if (filter.UnitId is long unit && !await db.CurriculumUnits.AnyAsync(u => u.CurriculumUnitId == unit && (!filter.Grade.HasValue || u.Grade == filter.Grade), ct)) return null;
        if (filter.AreaId is long area && !await db.KnowledgeAreas.AnyAsync(a => a.KnowledgeAreaId == area
            && (!filter.UnitId.HasValue || a.CurriculumUnitId == filter.UnitId) && (!filter.Grade.HasValue || a.CurriculumUnit.Grade == filter.Grade), ct)) return null;
        var units = await db.CurriculumUnits.AsNoTracking().Where(u => (!filter.Grade.HasValue || u.Grade == filter.Grade)
            && (!filter.UnitId.HasValue || u.CurriculumUnitId == filter.UnitId)
            && (!filter.AreaId.HasValue || u.KnowledgeAreas.Any(a => a.KnowledgeAreaId == filter.AreaId)))
            .Include(u => u.KnowledgeAreas).ThenInclude(a => a.Subskills).AsSplitQuery()
            .OrderBy(u => u.Grade).ThenBy(u => u.UnitNumber).ToListAsync(ct);
        return new(filter, units.Select(u => new TaxonomyUnit(u.CurriculumUnitId, u.Grade, u.UnitNumber, u.UnitName,
            u.KnowledgeAreas.Where(a => !filter.AreaId.HasValue || a.KnowledgeAreaId == filter.AreaId).OrderBy(a => a.SortOrder)
                .Select(a => new TaxonomyArea(a.KnowledgeAreaId, a.KnowledgeAreaCode, a.KnowledgeAreaName, a.Status == "Active",
                    a.Subskills.OrderBy(s => s.SortOrder).Select(s => new TaxonomySkill(s.SubskillId, s.SubskillCode, s.SubskillName, s.Description, a.Status == "Active" && s.Status == "Active")).ToArray())).ToArray())).ToArray());
    }
    private static IQueryable<SubskillChoice> Skills(HarmoniaDbContext db) => db.Subskills.AsNoTracking()
        .Where(s => s.Status == "Active" && s.KnowledgeArea.Status == "Active")
        .OrderBy(s => s.KnowledgeArea.CurriculumUnit.Grade).ThenBy(s => s.KnowledgeArea.CurriculumUnit.UnitNumber).ThenBy(s => s.SortOrder)
        .Select(s => new SubskillChoice(s.SubskillId, s.KnowledgeArea.CurriculumUnit.Grade,
            s.KnowledgeArea.CurriculumUnit.UnitName + " / " + s.KnowledgeArea.KnowledgeAreaName + " / " + s.SubskillCode + " — " + s.SubskillName));
    private static IQueryable<QuestionListRow> QuestionRows(IQueryable<Question> query) => query.Select(q => new QuestionListRow(q.QuestionId,
        q.QuestionCode, q.QuestionText, q.SubskillId, q.Subskill.SubskillName, q.Subskill.KnowledgeArea.CurriculumUnit.Grade,
        q.DifficultyLevel, q.Status, q.QuestionInTests.Count) { UsagePurpose = q.UsagePurpose });
    public async Task<QuestionBankPage?> GetQuestionsAsync(QuestionSearch search, CancellationToken ct = default)
    {
        if (Validate(search) is not null) return null;
        await using var db = await factory.CreateDbContextAsync(ct);
        string? notice = null, caption = null, unitName = null, areaName = null;
        if (search.ClearSubskill) { search.UnitId = search.AreaId = search.SubskillId = null; search.Page = 1; notice = "Đã bỏ lọc Subskill khi thay đổi khối. Bạn có thể chọn lại kỹ năng từ cây kiến thức."; }
        if (search.UnitId is long unitId)
        {
            var unit = await db.CurriculumUnits.AsNoTracking().Where(u => u.CurriculumUnitId == unitId).Select(u => new { u.Grade, u.UnitName }).SingleOrDefaultAsync(ct);
            if (unit is null || search.Grade.HasValue && unit.Grade != search.Grade)
            { search.UnitId = search.AreaId = search.SubskillId = null; search.Page = 1; notice = "Nhánh cũ không còn phù hợp; đã bỏ bộ lọc kiến thức. Hãy chọn lại nhánh."; }
            else { search.Grade ??= unit.Grade; unitName = unit.UnitName; }
        }
        if (search.AreaId is long areaId)
        {
            var area = await db.KnowledgeAreas.AsNoTracking().Where(a => a.KnowledgeAreaId == areaId)
                .Select(a => new { Grade = a.CurriculumUnit.Grade, a.CurriculumUnitId, a.CurriculumUnit.UnitName, a.KnowledgeAreaName }).SingleOrDefaultAsync(ct);
            if (area is null || search.Grade.HasValue && area.Grade != search.Grade || search.UnitId.HasValue && area.CurriculumUnitId != search.UnitId)
            { search.AreaId = search.SubskillId = null; search.Page = 1; notice = "Nhóm kiến thức cũ không phù hợp; hãy chọn lại trong cây kiến thức."; }
            else { search.UnitId = area.CurriculumUnitId; search.Grade ??= area.Grade; unitName = area.UnitName; areaName = area.KnowledgeAreaName; }
        }
        if (search.SubskillId is long id)
        {
            var skill = await db.Subskills.AsNoTracking().Where(s => s.SubskillId == id)
                .Select(s => new { Grade = s.KnowledgeArea.CurriculumUnit.Grade, s.KnowledgeAreaId, s.KnowledgeArea.CurriculumUnitId, s.KnowledgeArea.CurriculumUnit.UnitName, s.KnowledgeArea.KnowledgeAreaName, s.SubskillCode, s.SubskillName }).SingleOrDefaultAsync(ct);
            if (skill is null || search.Grade.HasValue && skill.Grade != search.Grade
                || search.UnitId.HasValue && skill.CurriculumUnitId != search.UnitId || search.AreaId.HasValue && skill.KnowledgeAreaId != search.AreaId)
            { search.SubskillId = null; search.Page = 1; notice = "Subskill cũ không còn phù hợp với khối hoặc không tồn tại; đã bỏ bộ lọc này. Hãy chọn lại nếu cần."; }
            else { caption = skill.SubskillCode + " — " + skill.SubskillName; search.UnitId = skill.CurriculumUnitId; search.AreaId = skill.KnowledgeAreaId; search.Grade ??= skill.Grade; unitName = skill.UnitName; areaName = skill.KnowledgeAreaName; }
        }
        var term = search.Term?.Trim().ToLowerInvariant() ?? "";
        var query = db.Questions.AsNoTracking().Where(q => (!search.Grade.HasValue || q.Subskill.KnowledgeArea.CurriculumUnit.Grade == search.Grade)
            && (!search.UnitId.HasValue || q.Subskill.KnowledgeArea.CurriculumUnitId == search.UnitId)
            && (!search.AreaId.HasValue || q.Subskill.KnowledgeAreaId == search.AreaId)
            && (!search.SubskillId.HasValue || q.SubskillId == search.SubskillId)
            && (term == "" || q.QuestionCode.ToLower().Contains(term) || q.QuestionText.ToLower().Contains(term)));
        if (!string.IsNullOrEmpty(search.UsagePurpose)) query = query.Where(q => q.UsagePurpose == search.UsagePurpose);
        return new(search, await QuestionRows(query.OrderByDescending(q => q.QuestionId).Skip((search.Page - 1) * 20).Take(20)).ToListAsync(ct), await query.CountAsync(ct), notice, caption) { KnowledgePath = string.Join(" / ", new[] { search.Grade.HasValue ? "Khối " + search.Grade : "Tất cả các khối", unitName, areaName, caption }.Where(x => x is not null)) };
    }
    public async Task<QuestionCreatePage> GetQuestionFormAsync(QuestionInput input, CancellationToken ct = default)
    { await using var db = await factory.CreateDbContextAsync(ct); return new(input, await Skills(db).ToListAsync(ct)); }
    public async Task<QuestionDetailsPage?> GetQuestionAsync(long id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Questions.AsNoTracking().Where(q => q.QuestionId == id).Select(q => new QuestionDetailsPage(q.QuestionId,
            new QuestionInput { QuestionCode = q.QuestionCode, QuestionText = q.QuestionText, OptionA = q.OptionA, OptionB = q.OptionB,
                OptionC = q.OptionC, OptionD = q.OptionD, AnswerKey = q.AnswerKey, DifficultyLevel = q.DifficultyLevel,
                SubskillId = q.SubskillId, Explanation = q.Explanation, UsagePurpose = q.UsagePurpose }, q.Subskill.SubskillCode + " — " + q.Subskill.SubskillName,
            q.Subskill.KnowledgeArea.CurriculumUnit.Grade, q.Status, q.QuestionInTests.Count) { UnitId = q.Subskill.KnowledgeArea.CurriculumUnitId, AreaId = q.Subskill.KnowledgeAreaId }).SingleOrDefaultAsync(ct);
    }
    public async Task<TestComposePage> GetTestFormAsync(TestInput input, string? search = null, CancellationToken ct = default, long? editId = null)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        input.Picks ??= [];
        input.Search = search ?? input.Search;
        var selected = input.Picks.Where(p => p is not null && p.Number.HasValue).ToList();
        var ids = selected.Select(p => p.QuestionId).Distinct().ToArray();
        var term = input.Search?.Trim().ToLowerInvariant() ?? "";
        var query = db.Questions.AsNoTracking().Where(q => q.UsagePurpose == "ForTest" && q.Status == "Active" && q.Subskill.Status == "Active" && q.Subskill.KnowledgeArea.Status == "Active"
            && q.Subskill.KnowledgeArea.CurriculumUnit.Grade == input.Grade && (term == "" || q.QuestionCode.ToLower().Contains(term) || q.QuestionText.ToLower().Contains(term)));
        query = query.Where(q => !ids.Contains(q.QuestionId));
        var capacity = Math.Max(0, 500 - selected.Count);
        var more = await query.CountAsync(ct) > capacity;
        var candidates = await QuestionRows(query.OrderByDescending(q => q.QuestionId).Take(capacity)).ToListAsync(ct);
        var questions = await QuestionRows(db.Questions.AsNoTracking().Where(q => ids.Contains(q.QuestionId))).ToListAsync(ct);
        if (editId.HasValue)
        {
            var retained = await Snapshots(db, editId.Value).ToDictionaryAsync(q => q.QuestionId, ct);
            questions = questions.Select(q => retained.TryGetValue(q.Id, out var row)
                ? q with { Text = row.Text, SubskillId = row.SubskillId, SubskillName = row.SubskillName, Grade = row.Grade } : q).ToList();
        }
        questions.AddRange(candidates);
        input.Picks = selected.Concat(candidates.Select(q => new TestPick { QuestionId = q.Id })).ToList();
        return new(input, input.Search, questions, more);
    }
    public async Task<IReadOnlyList<TestListRow>> GetTestsAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Tests.AsNoTracking().Where(access != null ? access.TestScope() : t => false).OrderByDescending(t => t.TestId)
            .Select(t => new TestListRow(t.TestId, t.TestName, t.Status, t.QuestionInTests.Count, t.Assessments.Count)).ToListAsync(ct);
    }
    private static IQueryable<TestSnapshotRow> Snapshots(HarmoniaDbContext db, long id) => db.QuestionInTests.AsNoTracking().Where(q => q.TestId == id)
        .OrderBy(q => q.QuestionNumber).Select(q => new TestSnapshotRow(q.QuestionId, q.QuestionNumber, q.SnSquestionText,
            q.SnSoptionA, q.SnSoptionB, q.SnSoptionC, q.SnSoptionD, q.SnSanswerKey, q.SnSsubskillId,
            q.SnSsubskill.SubskillCode, q.SnSsubskill.SubskillName, q.SnSsubskill.KnowledgeArea.CurriculumUnit.Grade));
    public async Task<TestDetailsPage?> GetTestAsync(long id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var test = await db.Tests.AsNoTracking().Where(access != null ? access.TestScope() : t => false).Where(t => t.TestId == id).Select(t => new { t.TestName, t.Description, t.Status, Count = t.Assessments.Count }).SingleOrDefaultAsync(ct);
        return test is null ? null : new(id, test.TestName, test.Description, test.Status, test.Count, await Snapshots(db, id).ToListAsync(ct));
    }
    private static string Fingerprint(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    private static QuestionInput QuestionFields(Question q) => new() { QuestionCode = q.QuestionCode, QuestionText = q.QuestionText,
        OptionA = q.OptionA, OptionB = q.OptionB, OptionC = q.OptionC, OptionD = q.OptionD, AnswerKey = q.AnswerKey,
        SubskillId = q.SubskillId, DifficultyLevel = q.DifficultyLevel, Explanation = q.Explanation, UsagePurpose = q.UsagePurpose };
    private static string QuestionVersion(QuestionInput fields, string status) => Fingerprint(new { fields, status });
    private static string TestVersion(TestDetailsPage page) => Fingerprint(new { page.Id, page.Name, page.Description, page.Status, page.Questions });
    public async Task<QuestionEditPage?> GetQuestionEditAsync(long id, CancellationToken ct = default)
    {
        if (actor?.IsManager != true) return null;
        var page = await GetQuestionAsync(id, ct);
        if (page is null || page.TestCount != 0) return null;
        var form = await GetQuestionFormAsync(page.Question, ct);
        return new(id, form.Input, form.Subskills, QuestionVersion(page.Question, page.Status));
    }
    public Task<AuthoringWriteResult> EditQuestionAsync(long id, QuestionInput input, string? version, CancellationToken ct = default) => Write(async db =>
    {
        if (actor?.IsManager != true) return new(false, "Chỉ quản lý học vụ được sửa trực tiếp câu hỏi chưa dùng. Hãy tạo bản sao để điều chỉnh.");
        if (ValidateQuestion(input) is string error) return new(false, error);
        // UPDATE lock also conflicts with FK KEY SHARE when a concurrent writer first uses this Question.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Question\" WHERE \"Question_ID\"={id} FOR UPDATE", ct);
        var question = await db.Questions.SingleOrDefaultAsync(q => q.QuestionId == id, ct);
        if (question is null) return new(false, "Không tìm thấy câu hỏi.");
        // All downstream usage (Assessment/Response) passes through QuestionInTest, including Draft links.
        if (await db.QuestionInTests.AnyAsync(q => q.QuestionId == id, ct)) return new(false, "Câu hỏi đã nằm trong đề, kể cả đề nháp; không thể sửa. Hãy tạo câu mới.");
        if (string.IsNullOrEmpty(version) || version != QuestionVersion(QuestionFields(question), question.Status))
            return new(false, "Câu hỏi đã thay đổi ở phiên khác. Tải lại trang sửa trước khi lưu.");
        await LockSkills(db, [input.SubskillId], ct);
        if (!await db.Subskills.AnyAsync(s => s.SubskillId == input.SubskillId && s.Status == "Active" && s.KnowledgeArea.Status == "Active", ct))
            return new(false, "Subskill hoặc nhóm kiến thức không tồn tại/đã ngừng hoạt động.");
        if (await db.Questions.AnyAsync(q => q.QuestionId != id && q.QuestionCode == input.QuestionCode, ct)) return new(false, "Mã câu hỏi đã tồn tại.");
        question.QuestionCode = input.QuestionCode; question.QuestionText = input.QuestionText; question.OptionA = input.OptionA;
        question.OptionB = input.OptionB; question.OptionC = input.OptionC; question.OptionD = input.OptionD;
        question.AnswerKey = input.AnswerKey; question.SubskillId = input.SubskillId;
        question.DifficultyLevel = input.DifficultyLevel; question.Explanation = input.Explanation ?? "";
        question.UsagePurpose = input.UsagePurpose;
        return new(true, "Đã cập nhật câu hỏi chưa được sử dụng.", id);
    }, ct);
    public async Task<TestComposePage?> GetTestEditAsync(long id, CancellationToken ct = default)
    {
        var page = await GetTestAsync(id, ct);
        if (page is null || page.Status != "Draft" || page.AssessmentCount != 0) return null;
        await using var db = await factory.CreateDbContextAsync(ct);
        if (await db.QuestionInTests.AnyAsync(q => q.TestId == id && q.Responses.Any(), ct)) return null;
        var input = new TestInput { TestName = page.Name, Description = page.Description, Grade = page.Questions.FirstOrDefault()?.Grade ?? 9,
            Version = TestVersion(page), Picks = page.Questions.Select(q => new TestPick { QuestionId = q.QuestionId, Number = q.Number }).ToList() };
        return (await GetTestFormAsync(input, ct: ct, editId: id)) with { EditId = id };
    }
    public Task<AuthoringWriteResult> EditTestAsync(long id, TestInput input, CancellationToken ct = default) => Write(async db =>
    {
        input.TestName = input.TestName?.Trim() ?? ""; input.Description = input.Description?.Trim();
        if (ValidatePicks(input) is string error) return new(false, error);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Test\" WHERE \"Test_ID\"={id} FOR UPDATE", ct);
        var test = await db.Tests.SingleOrDefaultAsync(t => t.TestId == id, ct);
        if (test is null || actor == null || !actor.IsManager && test.CreatedByUserId != actor.Id || test.Status != "Draft" || await db.Assessments.AnyAsync(a => a.TestId == id, ct))
            return new(false, "Chỉ được sửa đề Draft chưa có Assessment. Đề Published/Archived và đề đã dùng được giữ nguyên.");
        // Lock child rows before checking Response, so a new FK reference cannot slip between check and replacement.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"QuestionInTest\" WHERE \"Test_ID\"={id} ORDER BY \"QuestionInTest_ID\" FOR UPDATE", ct);
        if (await db.QuestionInTests.AnyAsync(q => q.TestId == id && q.Responses.Any(), ct)) return new(false, "Đề đã có Response; không thể sửa.");
        var old = await Snapshots(db, id).ToListAsync(ct);
        var current = new TestDetailsPage(id, test.TestName, test.Description, test.Status, 0, old);
        if (string.IsNullOrEmpty(input.Version) || input.Version != TestVersion(current))
            return new(false, "Đề đã thay đổi ở phiên khác. Tải lại trang sửa trước khi lưu.");
        var picks = input.Picks.Where(p => p.Number.HasValue).OrderBy(p => p.Number).ToArray();
        var oldById = old.ToDictionary(q => q.QuestionId);
        var newIds = picks.Where(p => !oldById.ContainsKey(p.QuestionId)).Select(p => p.QuestionId).Order().ToArray();
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Question\" WHERE \"Question_ID\"=ANY({newIds}) ORDER BY \"Question_ID\" FOR SHARE", ct);
        var added = await db.Questions.AsNoTracking().Where(q => newIds.Contains(q.QuestionId)).ToListAsync(ct);
        var skillIds = old.Where(q => picks.Any(p => p.QuestionId == q.QuestionId)).Select(q => q.SubskillId)
            .Concat(added.Select(q => q.SubskillId)).Distinct().Order().ToArray();
        await LockSkills(db, skillIds, ct);
        if (added.Count != newIds.Length || added.Any(q => q.Status != "Active" || q.UsagePurpose != "ForTest")
            || await db.Questions.CountAsync(q => newIds.Contains(q.QuestionId) && q.Subskill.Status == "Active"
                && q.Subskill.KnowledgeArea.Status == "Active" && q.Subskill.KnowledgeArea.CurriculumUnit.Grade == input.Grade, ct) != newIds.Length
            || await db.Subskills.CountAsync(s => skillIds.Contains(s.SubskillId) && s.KnowledgeArea.CurriculumUnit.Grade == input.Grade, ct) != skillIds.Length)
            return new(false, "Chỉ chọn câu Đánh giá đang hoạt động, đúng khối; không dùng câu luyện tập can thiệp. Danh sách đã chọn được giữ để bạn sửa.");
        var replacements = new List<QuestionInTest>();
        foreach (var pick in picks)
        {
            if (oldById.TryGetValue(pick.QuestionId, out var row)) replacements.Add(new() { TestId = id, QuestionId = row.QuestionId,
                QuestionNumber = pick.Number!.Value, SnSquestionText = row.Text, SnSoptionA = row.A, SnSoptionB = row.B,
                SnSoptionC = row.C, SnSoptionD = row.D, SnSanswerKey = row.Key, SnSsubskillId = row.SubskillId });
            else { var snapshot = Snapshot(added.Single(q => q.QuestionId == pick.QuestionId), pick.Number!.Value); snapshot.TestId = id; replacements.Add(snapshot); }
        }
        if (replacements.Any(q => q.SnSanswerKey is not ("A" or "B" or "C" or "D")
            || new[] { q.SnSquestionText, q.SnSoptionA, q.SnSoptionB, q.SnSoptionC, q.SnSoptionD }.Any(string.IsNullOrWhiteSpace)))
            return new(false, "Có snapshot hoặc câu nguồn thiếu nội dung/đáp án. Không cập nhật một phần.");
        // Replace only unused Draft children inside this transaction: avoids non-deferrable unique number collisions on swaps.
        await db.QuestionInTests.Where(q => q.TestId == id).ExecuteDeleteAsync(ct);
        db.QuestionInTests.AddRange(replacements); test.TestName = input.TestName; test.Description = input.Description;
        test.UpdatedAt = DateTime.UtcNow; test.UpdatedByUserId = actor!.RequiredId;
        await db.SaveChangesAsync(ct);
        return new(true, "Đã cập nhật đề nháp và thứ tự. Câu giữ lại bảo toàn snapshot, câu thêm mới đã chụp snapshot.", id);
    }, ct);
    public async Task<AssessmentCreatePage> GetAssessmentFormAsync(AssessmentCreateInput input, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return new(input,
            await db.Classes.AsNoTracking().Where(access != null ? access.ClassScope() : c => false).Where(c => c.Status == "Active").OrderBy(c => c.ClassCode).Select(c => new AuthoringClass(c.ClassId, c.ClassName, c.Grade, c.AcademicYear)).ToListAsync(ct),
            await db.Tests.AsNoTracking().Where(access != null ? access.TestScope() : t => false).Where(t => t.Status == "Published").OrderByDescending(t => t.TestId).Select(t => new TestListRow(t.TestId, t.TestName, t.Status, t.QuestionInTests.Count, t.Assessments.Count)).ToListAsync(ct),
            await db.Teachers.AsNoTracking().Where(t => actor != null && (actor.IsManager || t.TeacherId == actor.TeacherId)).Where(t => t.Status == "Active").OrderBy(t => t.TeacherCode).Select(t => new AuthoringTeacher(t.TeacherId, t.TeacherCode, t.FullName)).ToListAsync(ct),
            await db.TeacherClassAssignments.AsNoTracking().Where(a => actor != null && (actor.IsManager || a.TeacherId == actor.TeacherId)).Select(a => new AuthoringAssignment(a.ClassId, a.TeacherId, a.StartDate, a.EndDate)).ToListAsync(ct));
    }

    private static Task<int> LockSkills(HarmoniaDbContext db, long[] ids, CancellationToken ct) => db.Database.ExecuteSqlInterpolatedAsync($"SELECT s.\"Subskill_ID\" FROM \"Subskill\" s JOIN \"KnowledgeArea\" k ON k.\"KnowledgeArea_ID\"=s.\"KnowledgeArea_ID\" JOIN \"CurriculumUnit\" u ON u.\"CurriculumUnit_ID\"=k.\"CurriculumUnit_ID\" WHERE s.\"Subskill_ID\"=ANY({ids}) ORDER BY s.\"Subskill_ID\" FOR SHARE OF s,k,u", ct);
    public Task<AuthoringWriteResult> CreateQuestionAsync(QuestionInput input, CancellationToken ct = default) => Write(async db =>
    {
        if (ValidateQuestion(input) is string error) return new(false, error);
        await LockSkills(db, [input.SubskillId], ct);
        if (!await db.Subskills.AnyAsync(s => s.SubskillId == input.SubskillId && s.Status == "Active" && s.KnowledgeArea.Status == "Active", ct))
            return new(false, "Subskill không tồn tại hoặc Subskill/nhóm kiến thức đã ngừng hoạt động.");
        if (await db.Questions.AnyAsync(q => q.QuestionCode == input.QuestionCode, ct)) return new(false, "Mã câu hỏi đã tồn tại.");
        var question = new Question { QuestionCode = input.QuestionCode, QuestionText = input.QuestionText, OptionA = input.OptionA,
            OptionB = input.OptionB, OptionC = input.OptionC, OptionD = input.OptionD, AnswerKey = input.AnswerKey,
            DifficultyLevel = input.DifficultyLevel, SubskillId = input.SubskillId, Explanation = input.Explanation ?? "", Status = "Active", UsagePurpose = input.UsagePurpose };
        db.Questions.Add(question); await db.SaveChangesAsync(ct);
        return new(true, "Đã tạo câu hỏi trong ngân hàng dùng chung. Giáo viên muốn điều chỉnh hãy tạo bản sao; chỉ quản lý được sửa câu chưa dùng.", question.QuestionId);
    }, ct);
    internal static QuestionInTest Snapshot(Question q, short number) => new() { QuestionId = q.QuestionId, QuestionNumber = number,
        SnSquestionText = q.QuestionText, SnSoptionA = q.OptionA, SnSoptionB = q.OptionB, SnSoptionC = q.OptionC, SnSoptionD = q.OptionD,
        SnSanswerKey = q.AnswerKey, SnSsubskillId = q.SubskillId };
    public Task<AuthoringWriteResult> CreateTestAsync(TestInput input, CancellationToken ct = default) => Write(async db =>
    {
        input.TestName = input.TestName?.Trim() ?? ""; input.Description = input.Description?.Trim();
        if (ValidatePicks(input) is string error) return new(false, error);
        var picks = input.Picks.Where(p => p.Number.HasValue).OrderBy(p => p.Number).ToArray(); var ids = picks.Select(p => p.QuestionId).Order().ToArray();
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Question\" WHERE \"Question_ID\"=ANY({ids}) ORDER BY \"Question_ID\" FOR SHARE", ct);
        var questions = await db.Questions.AsNoTracking().Where(q => ids.Contains(q.QuestionId)).ToListAsync(ct);
        await LockSkills(db, questions.Select(q => q.SubskillId).Distinct().Order().ToArray(), ct);
        if (questions.Count != picks.Length || questions.Any(q => q.Status != "Active" || q.UsagePurpose != "ForTest")
            || await db.Questions.CountAsync(q => ids.Contains(q.QuestionId) && q.Subskill.Status == "Active" && q.Subskill.KnowledgeArea.Status == "Active"
                && q.Subskill.KnowledgeArea.CurriculumUnit.Grade == input.Grade, ct) != picks.Length)
            return new(false, "Chỉ chọn câu Đánh giá đang hoạt động, đúng khối. Câu luyện tập can thiệp không được đưa vào đề. Không tạo một phần đề.");
        if (questions.Any(q => q.AnswerKey is not ("A" or "B" or "C" or "D")
            || new[] { q.QuestionText, q.OptionA, q.OptionB, q.OptionC, q.OptionD }.Any(string.IsNullOrWhiteSpace)))
            return new(false, "Có câu hỏi nguồn thiếu nội dung/lựa chọn hoặc key chưa hợp lệ. Không tạo đề từ dữ liệu này.");
        var test = new Test { TestName = input.TestName, Description = input.Description, Status = "Draft", CreatedByUserId = actor!.RequiredId };
        foreach (var pick in picks) test.QuestionInTests.Add(Snapshot(questions.Single(q => q.QuestionId == pick.QuestionId), pick.Number!.Value));
        db.Tests.Add(test); await db.SaveChangesAsync(ct);
        return new(true, "Đã tạo đề nháp và lưu snapshot đầy đủ. Kiểm tra trước khi phát hành.", test.TestId);
    }, ct);
    public Task<AuthoringWriteResult> PublishTestAsync(long id, CancellationToken ct = default) => Write(async db =>
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Test\" WHERE \"Test_ID\"={id} FOR UPDATE", ct);
        var test = await db.Tests.SingleOrDefaultAsync(t => t.TestId == id, ct);
        if (test is null) return new(false, "Không tìm thấy đề.");
        if (actor == null || !actor.IsManager && test.CreatedByUserId != actor.Id || test.Status != "Draft" || await db.Assessments.AnyAsync(a => a.TestId == id, ct)
            || await db.QuestionInTests.AnyAsync(q => q.TestId == id && q.Responses.Any(), ct)) return new(false, "Chỉ phát hành đề nháp chưa được sử dụng. Đề cũ được giữ nguyên.");
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"QuestionInTest\" WHERE \"Test_ID\"={id} ORDER BY \"QuestionInTest_ID\" FOR SHARE", ct);
        if (ValidateSnapshots(await Snapshots(db, id).ToListAsync(ct)) is string error) return new(false, error);
        test.Status = "Published"; test.UpdatedAt = DateTime.UtcNow; test.UpdatedByUserId = actor!.RequiredId;
        return new(true, "Đã phát hành đề kiểm tra. Bạn có thể dùng đề này để tạo đợt đánh giá.", id);
    }, ct);
    public Task<AuthoringWriteResult> CopyTestAsync(long id, CopyTestInput input, CancellationToken ct = default) => Write(async db =>
    {
        input.TestName = input.TestName?.Trim() ?? ""; input.Description = input.Description?.Trim();
        if (Validate(input) is string error) return new(false, error);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Test\" WHERE \"Test_ID\"={id} FOR SHARE", ct);
        if (access == null || !await access.TestAsync(id, false, ct) || !await db.Tests.AnyAsync(t => t.TestId == id, ct)) return new(false, "Không tìm thấy đề nguồn.");
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"QuestionInTest\" WHERE \"Test_ID\"={id} ORDER BY \"QuestionInTest_ID\" FOR SHARE", ct);
        var rows = await Snapshots(db, id).ToListAsync(ct);
        if (ValidateSnapshots(rows) is string snapshotError) return new(false, snapshotError);
        var test = new Test { TestName = input.TestName, Description = input.Description, Status = "Draft", CreatedByUserId = actor!.RequiredId };
        foreach (var row in rows) test.QuestionInTests.Add(new() { QuestionId = row.QuestionId, QuestionNumber = row.Number, SnSquestionText = row.Text,
            SnSoptionA = row.A, SnSoptionB = row.B, SnSoptionC = row.C, SnSoptionD = row.D, SnSanswerKey = row.Key, SnSsubskillId = row.SubskillId });
        db.Tests.Add(test); await db.SaveChangesAsync(ct);
        return new(true, "Đã tạo bản sao nháp từ snapshot gốc; không lấy lại nội dung Question hiện hành.", test.TestId);
    }, ct);
    public Task<AuthoringWriteResult> CreateAssessmentAsync(AssessmentCreateInput input, CancellationToken ct = default) => Write(async db =>
    {
        input.AssessmentName = input.AssessmentName?.Trim() ?? "";
        if (actor == null || access == null || !actor.IsManager && input.TeacherId != actor.TeacherId || !await access.ClassAsync(input.ClassId, ct)) return new(false, "Không có quyền tạo Assessment cho lớp/giáo viên này.");
        if (Validate(input) is string error) return new(false, error);
        if (AssessmentFollowupPlanRules.ValidateDate(input.AssessmentDate!.Value, input.PlannedFollowupDate) is string plannedError)
            return new(false, plannedError);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Test\" WHERE \"Test_ID\"={input.TestId} FOR SHARE", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Class\" WHERE \"Class_ID\"={input.ClassId} FOR SHARE", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Teacher\" WHERE \"Teacher_ID\"={input.TeacherId} FOR SHARE", ct);
        var cls = await db.Classes.AsNoTracking().SingleOrDefaultAsync(c => c.ClassId == input.ClassId && c.Status == "Active", ct);
        if (cls is null || !await db.Teachers.AnyAsync(t => t.TeacherId == input.TeacherId && t.Status == "Active", ct)
            || !await db.Tests.AnyAsync(t => t.TestId == input.TestId && t.Status == "Published", ct))
            return new(false, "Chọn lớp/giáo viên đang hoạt động và đề đã Published; dữ liệu có thể vừa thay đổi.");
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"TeacherClassAssignment\" WHERE \"Class_ID\"={input.ClassId} AND \"Teacher_ID\"={input.TeacherId} ORDER BY \"TeacherClassAssignment_ID\" FOR SHARE", ct);
        var date = input.AssessmentDate!.Value;
        if (!await db.TeacherClassAssignments.AnyAsync(a => a.ClassId == input.ClassId && a.TeacherId == input.TeacherId
            && a.StartDate <= date && (a.EndDate == null || a.EndDate > date), ct))
            return new(false, "Giáo viên chưa được phân công lớp tại ngày đánh giá. StartDate được tính, EndDate không được tính; không tự tạo phân công.");
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"QuestionInTest\" WHERE \"Test_ID\"={input.TestId} ORDER BY \"QuestionInTest_ID\" FOR SHARE", ct);
        var rows = await Snapshots(db, input.TestId).ToListAsync(ct);
        if (ValidateSnapshots(rows) is string snapshotError) return new(false, snapshotError);
        await LockSkills(db, rows.Select(q => q.SubskillId).Distinct().Order().ToArray(), ct);
        // Read grades again after locking taxonomy; never use current Question mapping.
        if (await db.QuestionInTests.CountAsync(q => q.TestId == input.TestId && q.SnSsubskill.KnowledgeArea.CurriculumUnit.Grade == cls.Grade, ct) != rows.Count)
            return new(false, "Khối của các Subskill snapshot trong đề không khớp khối lớp.");
        var assessment = new Assessment { AssessmentName = input.AssessmentName, ClassId = input.ClassId, TestId = input.TestId,
            TeacherId = input.TeacherId, AssessmentDate = date, PlannedFollowupDate = input.PlannedFollowupDate, Status = "Open", CreatedByUserId = actor!.RequiredId };
        db.Assessments.Add(assessment); await db.SaveChangesAsync(ct);
        return new(true, "Đã tạo đợt đánh giá ở trạng thái “Đang mở”. Khi nhập kết quả, hệ thống sẽ kiểm tra danh sách học sinh thuộc lớp vào ngày đánh giá.", assessment.AssessmentId);
    }, ct);

    private async Task<AuthoringWriteResult> Write(Func<HarmoniaDbContext, Task<AuthoringWriteResult>> action, CancellationToken ct)
    {
        if (actor?.Id is null || !CanWrite) return new(false, "Chức năng chưa bật hoặc chưa đăng nhập.");
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var commitStarted = false;
        try
        {
            var result = await action(db); if (!result.Succeeded) return result;
            await db.SaveChangesAsync(ct); commitStarted = true; await tx.CommitAsync(ct); return result;
        }
        catch (Exception ex) when (ex is DbUpdateException or NpgsqlException or OperationCanceledException)
        {
            try { await tx.RollbackAsync(CancellationToken.None); } catch (Exception rollback) when (rollback is NpgsqlException or InvalidOperationException) { }
            if (commitStarted) return new(false, "Chưa xác nhận được kết quả lưu. Kiểm tra danh sách trước khi thử lại.");
            if (ex is DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } }) return new(false, "Mã hoặc liên kết đã tồn tại ở phiên khác. Không lưu một phần; tải lại danh sách.");
            return new(false, "Không lưu được thao tác; toàn bộ thay đổi trước commit đã rollback. Dữ liệu có thể vừa đổi, hãy kiểm tra và thử lại.");
        }
    }
}
