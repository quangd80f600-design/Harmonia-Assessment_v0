using Harmonia.Web.Data;
using Harmonia.Web.Models.Entities;
using Harmonia.Web.Services.Import;
using Harmonia.Web.Services.Security;
using Harmonia.Web.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Harmonia.Web.Services;

// Read-only composition. Existing analytics remain the source of calculation rules.
public sealed class DashboardService(HarmoniaDbContext db, BusinessAccess access, CurrentActor actor,
    AssessmentAnalysisService analysis, AssessmentImportService imports, AuthoringService authoring,
    IOptions<AuthoringOptions> authoringOptions, IOptions<AcademicManagementOptions> academicOptions,
    IOptions<InterventionManagementOptions> interventionOptions)
{
    private static readonly System.Linq.Expressions.Expression<Func<Assessment, DashboardAssessmentContext>> Context =
        a => new(a.AssessmentId, a.ClassId, a.AssessmentName, a.Class.ClassName, a.AssessmentDate, a.Status);

    public async Task<DashboardPage?> GetAsync(DashboardFilter filter, CancellationToken ct = default)
    {
        if (actor.Id is null || filter.ClassId is <= 0 || filter.AssessmentId is <= 0) return null;
        var today = AcademicManagementService.Today;
        var classes = db.Classes.AsNoTracking().Where(access.ClassScope());
        var assessments = db.Assessments.AsNoTracking().Where(access.AssessmentScope());
        if (filter.ClassId is long cid)
        {
            if (!await classes.AnyAsync(c => c.ClassId == cid, ct)) return null;
            assessments = assessments.Where(a => a.ClassId == cid);
        }
        DashboardAssessmentContext? selected;
        if (filter.AssessmentId is long aid)
        {
            selected = await assessments.Where(a => a.AssessmentId == aid).Select(Context).SingleOrDefaultAsync(ct);
            if (selected is null) return null;
        }
        else selected = await assessments.Where(a => a.Status != "Cancelled").OrderByDescending(a => a.AssessmentDate)
            .ThenByDescending(a => a.AssessmentId).Select(Context).FirstOrDefaultAsync(ct);

        var options = await assessments.OrderByDescending(a => a.AssessmentDate).ThenByDescending(a => a.AssessmentId)
            .Select(Context).Take(20).ToListAsync(ct);
        if (selected is not null && options.All(a => a.Id != selected.Id)) options.Add(selected);
        var classOptions = await classes.OrderByDescending(c => c.AcademicYear).ThenBy(c => c.ClassName).ThenBy(c => c.ClassId)
            .Select(c => new DashboardOption(c.ClassId, c.ClassName + " · " + c.AcademicYear)).ToListAsync(ct);
        var filteredClasses = classes.Where(c => filter.ClassId == null || c.ClassId == filter.ClassId);
        var classRows = await filteredClasses.OrderBy(c => c.Status != "Active").ThenByDescending(c => c.AcademicYear)
            .ThenBy(c => c.ClassName).ThenBy(c => c.ClassId).Take(6)
            .Select(c => new DashboardClassRow(c.ClassId, c.ClassCode, c.ClassName, c.Grade, c.AcademicYear, c.Status,
                c.ClassMemberships.Where(m => m.StartDate <= today && (m.EndDate == null || m.EndDate > today))
                    .Select(m => m.StudentId).Distinct().Count())).ToListAsync(ct);
        int? unassigned = actor.IsManager ? await filteredClasses.CountAsync(c => c.Status == "Active"
            && !c.TeacherClassAssignments.Any(t => t.StartDate <= today && (t.EndDate == null || t.EndDate > today)), ct) : null;

        var recent = new List<DashboardAssessmentRow>();
        // GetPageAsync already composes S4 completion and existing read/write/import eligibility.
        foreach (var item in options.OrderByDescending(a => a.Date).ThenByDescending(a => a.Id).Take(5))
        {
            var page = await imports.GetPageAsync(item.Id, ct);
            if (page is not null) recent.Add(new(item, page.ResultCompletion, page.CanImport));
        }
        var selectedImport = selected is null ? false : recent.FirstOrDefault(a => a.Assessment.Id == selected.Id)?.CanImport
            ?? (await imports.GetPageAsync(selected.Id, ct))?.CanImport == true;
        var summary = selected is null ? null : await analysis.GetSummaryAsync(selected.Id, ct);
        var skills = summary?.Rows.Where(r => r.IncorrectResponses > 0).Take(5).ToArray() ?? [];
        var enabled = interventionOptions.Value.Enabled;
        var packageRows = new List<DashboardPackageRow>();
        IReadOnlyList<DashboardFollowupEvent> followups = [];
        if (enabled)
        {
            var sourceIds = assessments.Where(a => selected == null || a.AssessmentId == selected.Id).Select(a => a.AssessmentId);
            packageRows = await db.InterventionPackages.AsNoTracking().Where(p => sourceIds.Contains(p.AssessmentId))
                .OrderByDescending(p => p.InterventionPackageId).Take(5)
                .Select(p => new DashboardPackageRow(p.InterventionPackageId, p.PackageName, p.Status, p.AssessmentId,
                    p.Assessment.AssessmentName, p.InterventionPackageStudents.Count(),
                    p.InterventionPackageStudents.Count(s => s.DeliveredDate == null))).ToListAsync(ct);
            followups = await ReadFollowupsAsync(sourceIds, today, ct);
        }
        AssessmentCreationGuidanceViewModel? creation = null;
        if (authoringOptions.Value.Enabled && (actor.IsManager || actor.CanTeach))
            creation = AssessmentCreationGuidanceViewModel.From(await authoring.GetAssessmentFormAsync(new(), ct), actor.IsManager);
        var planned = assessments.Where(a => a.PlannedFollowupDate != null && (a.Status == "Open" || a.Status == "Completed"));
        var writable = db.Assessments.Where(access.AssessmentWriteScope()).Select(a => a.AssessmentId);
        // Query dates before DTO projection: keeps nullable DateOnly ordering/filtering in PostgreSQL.
        var upcomingIds = await planned.Where(a => a.PlannedFollowupDate >= today).OrderBy(a => a.PlannedFollowupDate)
            .ThenBy(a => a.AssessmentId).Select(a => a.AssessmentId).Take(5).ToArrayAsync(ct);
        var pastIds = await planned.Where(a => a.PlannedFollowupDate < today).OrderByDescending(a => a.PlannedFollowupDate)
            .ThenBy(a => a.AssessmentId).Select(a => a.AssessmentId).Take(5).ToArrayAsync(ct);
        var shownIds = upcomingIds.Concat(pastIds).ToArray();
        var plannedRows = await planned.Where(a => shownIds.Contains(a.AssessmentId)).Select(a => new DashboardPlannedFollowup(a.AssessmentId, a.AssessmentName,
            a.Class.ClassName, a.AssessmentDate, a.PlannedFollowupDate!.Value, authoringOptions.Value.Enabled && writable.Contains(a.AssessmentId))).ToListAsync(ct);
        var byId = plannedRows.ToDictionary(p => p.AssessmentId);
        return new(today, actor.IsManager, new(filter.ClassId, selected?.Id), classOptions, options, classRows, unassigned,
            recent, selected, skills, summary?.Rows.Any(r => r.TotalResponses > 0) == true, packageRows, followups,
            enabled, academicOptions.Value.Enabled && actor.IsManager, creation, selectedImport)
        { PlannedFollowups = shownIds.Where(byId.ContainsKey).Select(id => byId[id]).ToArray() };
    }

    private sealed class FollowupCandidate
    {
        public long BaselineId { get; init; }
        public string BaselineName { get; init; } = "";
        public long FollowupId { get; init; }
        public string FollowupName { get; init; } = "";
        public string ClassName { get; init; } = "";
        public DateOnly Date { get; init; }
        public bool HasAnyAttempt { get; init; }
        public long TestId { get; init; }
        public short Grade { get; init; }
    }

    private async Task<IReadOnlyList<DashboardFollowupEvent>> ReadFollowupsAsync(IQueryable<long> sourceIds, DateOnly today, CancellationToken ct)
    {
        var readable = db.Assessments.Where(access.AssessmentScope());
        var links = db.InterventionPackageFollowups.AsNoTracking().Where(f => sourceIds.Contains(f.InterventionPackage.AssessmentId)
            && readable.Any(a => a.AssessmentId == f.FollowupAssessmentId)
            && f.FollowupAssessment.ClassId == f.InterventionPackage.Assessment.ClassId
            && f.FollowupAssessmentId != f.InterventionPackage.AssessmentId
            && f.FollowupAssessment.AssessmentDate > f.InterventionPackage.Assessment.AssessmentDate
            && (f.FollowupAssessment.Status == "Open" || f.FollowupAssessment.Status == "Completed"));
        var pairs = links.Select(f => new FollowupCandidate {
            BaselineId = f.InterventionPackage.AssessmentId, BaselineName = f.InterventionPackage.Assessment.AssessmentName,
            FollowupId = f.FollowupAssessmentId, FollowupName = f.FollowupAssessment.AssessmentName,
            ClassName = f.FollowupAssessment.Class.ClassName, Date = f.FollowupAssessment.AssessmentDate,
            HasAnyAttempt = f.FollowupAssessment.Attempts.Any(), TestId = f.FollowupAssessment.TestId,
            Grade = f.FollowupAssessment.Class.Grade }).Distinct();
        // Fill each section AFTER snapshot validation. Invalid historical links must not
        // consume its five slots and hide later valid events. Scan in bounded batches.
        var upcoming = await ValidCandidates(pairs.Where(p => p.Date >= today).OrderBy(p => p.Date)
            .ThenBy(p => p.FollowupId).ThenBy(p => p.BaselineId), ct);
        var overdue = await ValidCandidates(pairs.Where(p => p.Date < today && !p.HasAnyAttempt).OrderByDescending(p => p.Date)
            .ThenBy(p => p.FollowupId).ThenBy(p => p.BaselineId), ct);
        var candidates = upcoming.Concat(overdue).ToList();

        async Task<List<FollowupCandidate>> ValidCandidates(IQueryable<FollowupCandidate> ordered, CancellationToken token)
        {
            var valid = new List<FollowupCandidate>();
            for (var offset = 0; valid.Count < 5; offset += 10)
            {
                var batch = await ordered.Skip(offset).Take(10).ToListAsync(token);
                if (batch.Count == 0) break;
                var testIds = batch.Select(c => c.TestId).Distinct().ToArray();
                var snapshots = await db.QuestionInTests.AsNoTracking().Where(q => testIds.Contains(q.TestId))
                    .Select(q => new { q.TestId, Row = new TestSnapshotRow(q.QuestionId, q.QuestionNumber, q.SnSquestionText,
                        q.SnSoptionA, q.SnSoptionB, q.SnSoptionC, q.SnSoptionD, q.SnSanswerKey, q.SnSsubskillId,
                        q.SnSsubskill.SubskillCode, q.SnSsubskill.SubskillName, q.SnSsubskill.KnowledgeArea.CurriculumUnit.Grade) }).ToListAsync(token);
                var grouped = snapshots.GroupBy(q => q.TestId).ToDictionary(g => g.Key, g => g.Select(q => q.Row).ToArray());
                foreach (var item in batch)
                {
                    if (grouped.TryGetValue(item.TestId, out var rows) && AuthoringService.ValidateSnapshots(rows) is null
                        && rows.All(q => q.Grade == item.Grade)) valid.Add(item);
                    if (valid.Count == 5) break;
                }
                if (batch.Count < 10) break;
            }
            return valid;
        }
        var result = new List<DashboardFollowupEvent>();
        // Bounded 10 pairs; links projected without notes and without unscoped student data.
        var baseIds = candidates.Select(c => c.BaselineId).Distinct().ToArray();
        var followIds = candidates.Select(c => c.FollowupId).Distinct().ToArray();
        var packageLinks = await links.Where(f => baseIds.Contains(f.InterventionPackage.AssessmentId) && followIds.Contains(f.FollowupAssessmentId))
            .Select(f => new { BaselineId = f.InterventionPackage.AssessmentId, FollowupId = f.FollowupAssessmentId,
                Id = f.InterventionPackageId, Name = f.InterventionPackage.PackageName }).Distinct().ToListAsync(ct);
        foreach (var c in candidates)
        {
            var related = packageLinks.Where(p => p.BaselineId == c.BaselineId && p.FollowupId == c.FollowupId).OrderBy(p => p.Id).ToArray();
            result.Add(new(c.BaselineId, c.BaselineName, c.FollowupId, c.FollowupName, c.ClassName, c.Date,
                c.HasAnyAttempt, related.Take(3).Select(p => new DashboardPackageLink(p.Id, p.Name)).ToArray(), related.Length));
        }
        return result;
    }
}
