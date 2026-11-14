using Harmonia.Web.Data;
using Harmonia.Web.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Harmonia.Web.Services;

// Read-only workspace navigation. All writes remain in AuthoringService.
public sealed class TestWorkspaceService(IDbContextFactory<HarmoniaDbContext> factory)
{
    public async Task<IReadOnlyList<string>> RetainedIdsAsync(long testId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var ids = await db.QuestionInTests.AsNoTracking().Where(q => q.TestId == testId).Select(q => q.QuestionId).ToListAsync(ct);
        return ids.Select(id => id.ToString()).ToArray();
    }
    public async Task<WorkspaceNodes?> NodesAsync(WorkspaceTreeFilter filter, CancellationToken ct)
    {
        if (AuthoringService.Validate(filter) is not null) return null;
        await using var db = await factory.CreateDbContextAsync(ct);
        var skip = (filter.Page - 1) * 30;
        List<WorkspaceNode> rows;
        if (filter.Level == "unit")
        {
            var data = await db.CurriculumUnits.AsNoTracking().Where(u => u.Grade == filter.Grade)
                .OrderBy(u => u.UnitNumber).ThenBy(u => u.CurriculumUnitId).Skip(skip).Take(31)
                .Select(u => new { u.CurriculumUnitId, u.UnitNumber, u.UnitName, Count = u.KnowledgeAreas.Count(a => a.Status == "Active") }).ToListAsync(ct);
            rows = data.Select(u => new WorkspaceNode(u.CurriculumUnitId.ToString(), $"Bài {u.UnitNumber} — {u.UnitName}", "", "unit", u.Count, u.CurriculumUnitId.ToString())).ToList();
        }
        else if (filter.Level == "area" && filter.ParentId.HasValue)
        {
            var data = await db.KnowledgeAreas.AsNoTracking().Where(a => a.Status == "Active" && a.CurriculumUnitId == filter.ParentId && a.CurriculumUnit.Grade == filter.Grade)
                .OrderBy(a => a.SortOrder).ThenBy(a => a.KnowledgeAreaId).Skip(skip).Take(31)
                .Select(a => new { a.KnowledgeAreaId, a.KnowledgeAreaName, a.KnowledgeAreaCode, a.CurriculumUnitId, Count = a.Subskills.Count(s => s.Status == "Active") }).ToListAsync(ct);
            rows = data.Select(a => new WorkspaceNode(a.KnowledgeAreaId.ToString(), a.KnowledgeAreaName, a.KnowledgeAreaCode, "area", a.Count, a.CurriculumUnitId.ToString(), a.KnowledgeAreaId.ToString())).ToList();
        }
        else if (filter.Level == "skill" && filter.ParentId.HasValue)
        {
            var data = await db.Subskills.AsNoTracking().Where(s => s.Status == "Active" && s.KnowledgeArea.Status == "Active"
                && s.KnowledgeAreaId == filter.ParentId && s.KnowledgeArea.CurriculumUnit.Grade == filter.Grade)
                .OrderBy(s => s.SortOrder).ThenBy(s => s.SubskillId).Skip(skip).Take(31)
                .Select(s => new { s.SubskillId, s.SubskillName, s.SubskillCode, s.KnowledgeAreaId, s.KnowledgeArea.CurriculumUnitId }).ToListAsync(ct);
            rows = data.Select(s => new WorkspaceNode(s.SubskillId.ToString(), s.SubskillName, s.SubskillCode, "skill", 0, s.CurriculumUnitId.ToString(), s.KnowledgeAreaId.ToString())).ToList();
        }
        else return null;
        return new(rows.Take(30).ToArray(), rows.Count > 30);
    }
    public async Task<WorkspaceNodes?> SearchSkillsAsync(WorkspaceFilter filter, CancellationToken ct)
    {
        if (AuthoringService.Validate(filter) is not null) return null;
        await using var db = await factory.CreateDbContextAsync(ct);
        var term = filter.Search?.Trim().ToLowerInvariant() ?? "";
        var data = await db.Subskills.AsNoTracking().Where(s => s.Status == "Active" && s.KnowledgeArea.Status == "Active"
            && s.KnowledgeArea.CurriculumUnit.Grade == filter.Grade
            && (term == "" || s.SubskillCode.ToLower().Contains(term) || s.SubskillName.ToLower().Contains(term)))
            .OrderBy(s => s.KnowledgeArea.CurriculumUnit.UnitNumber).ThenBy(s => s.SortOrder).ThenBy(s => s.SubskillId)
            .Skip((filter.Page - 1) * 20).Take(21)
            .Select(s => new { s.SubskillId, s.SubskillCode, s.SubskillName, s.KnowledgeAreaId, s.KnowledgeArea.CurriculumUnitId,
                Path = s.KnowledgeArea.CurriculumUnit.UnitName + " / " + s.KnowledgeArea.KnowledgeAreaName }).ToListAsync(ct);
        return new(data.Take(20).Select(s => new WorkspaceNode(s.SubskillId.ToString(), s.SubskillName, s.SubskillCode, "skill", 0,
            s.CurriculumUnitId.ToString(), s.KnowledgeAreaId.ToString(), s.Path)).ToArray(), data.Count > 20);
    }
    public async Task<WorkspaceBank?> QuestionsAsync(WorkspaceFilter filter, CancellationToken ct)
    {
        if (AuthoringService.Validate(filter) is not null) return null;
        await using var db = await factory.CreateDbContextAsync(ct);
        // Validate the complete dependency chain; never silently query a different branch.
        if (filter.UnitId.HasValue && !await db.CurriculumUnits.AnyAsync(u => u.CurriculumUnitId == filter.UnitId && u.Grade == filter.Grade, ct)) return null;
        if (filter.AreaId.HasValue && !await db.KnowledgeAreas.AnyAsync(a => a.KnowledgeAreaId == filter.AreaId && a.Status == "Active"
            && a.CurriculumUnit.Grade == filter.Grade && (!filter.UnitId.HasValue || a.CurriculumUnitId == filter.UnitId), ct)) return null;
        if (filter.SubskillId.HasValue && !await db.Subskills.AnyAsync(s => s.SubskillId == filter.SubskillId && s.Status == "Active" && s.KnowledgeArea.Status == "Active"
            && s.KnowledgeArea.CurriculumUnit.Grade == filter.Grade && (!filter.UnitId.HasValue || s.KnowledgeArea.CurriculumUnitId == filter.UnitId)
            && (!filter.AreaId.HasValue || s.KnowledgeAreaId == filter.AreaId), ct)) return null;
        var term = filter.Search?.Trim().ToLowerInvariant() ?? "";
        var query = db.Questions.AsNoTracking().Where(q => q.UsagePurpose == "ForTest" && q.Status == "Active" && q.Subskill.Status == "Active" && q.Subskill.KnowledgeArea.Status == "Active"
            && q.Subskill.KnowledgeArea.CurriculumUnit.Grade == filter.Grade
            && (!filter.UnitId.HasValue || q.Subskill.KnowledgeArea.CurriculumUnitId == filter.UnitId)
            && (!filter.AreaId.HasValue || q.Subskill.KnowledgeAreaId == filter.AreaId)
            && (!filter.SubskillId.HasValue || q.SubskillId == filter.SubskillId)
            && (term == "" || q.QuestionCode.ToLower().Contains(term) || q.QuestionText.ToLower().Contains(term)));
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(q => q.QuestionId).Skip((filter.Page - 1) * 20).Take(20)
            .Select(q => new { q.QuestionId, q.QuestionCode, q.QuestionText, q.Subskill.SubskillName, Grade = q.Subskill.KnowledgeArea.CurriculumUnit.Grade,
                q.DifficultyLevel, q.OptionA, q.OptionB, q.OptionC, q.OptionD, q.AnswerKey }).ToListAsync(ct);
        string scope = "Khối " + filter.Grade;
        if (filter.SubskillId.HasValue) scope += " · " + await db.Subskills.Where(s => s.SubskillId == filter.SubskillId).Select(s => s.SubskillCode + " — " + s.SubskillName).SingleAsync(ct);
        else if (filter.AreaId.HasValue) scope += " · " + await db.KnowledgeAreas.Where(a => a.KnowledgeAreaId == filter.AreaId).Select(a => a.KnowledgeAreaName).SingleAsync(ct);
        else if (filter.UnitId.HasValue) scope += " · " + await db.CurriculumUnits.Where(u => u.CurriculumUnitId == filter.UnitId).Select(u => u.UnitName).SingleAsync(ct);
        return new(rows.Select(q => new WorkspaceQuestion(q.QuestionId.ToString(), q.QuestionCode, q.QuestionText, q.SubskillName, q.Grade,
            q.DifficultyLevel, q.OptionA, q.OptionB, q.OptionC, q.OptionD, q.AnswerKey)).ToArray(), total, filter.Page, 20, scope);
    }
}
