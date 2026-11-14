using System.Data;
using Harmonia.Web.Data;
using Harmonia.Web.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Harmonia.Web.Services;

public sealed class StudentReportService(HarmoniaDbContext db, IndividualDiagnosisService diagnosis,
    Microsoft.Extensions.Options.IOptions<WorksheetOptions>? worksheets = null,
    Microsoft.Extensions.Options.IOptions<ParentCommentOptions>? comments = null)
{
    public async Task<StudentReportViewModel?> GetReportAsync(long assessmentId, long studentId, CancellationToken ct = default)
    {
        if (assessmentId <= 0 || studentId <= 0) return null;
        // S1 and the public intervention projections share one consistent, read-only DB snapshot.
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", ct);
        var report = await BuildBaseAsync(assessmentId, studentId, ct);
        if (report is not null && comments?.Value.Enabled == true)
            report = report with { ApprovedComment = await ParentCommentEvidence.ReadApprovedAsync(db, report, ct, comments.Value.SeparateSections) };
        await tx.CommitAsync(ct);
        return report;
    }

    internal async Task<StudentReportViewModel?> BuildBaseAsync(long assessmentId, long studentId, CancellationToken ct)
    {
        var result = await diagnosis.GetDiagnosisAsync(assessmentId, studentId, ct);
        if (result is null) return null; // S1 enforces Assessment-date roster, including for students with an Attempt.
        var testId = await db.Assessments.Where(a => a.AssessmentId == assessmentId).Select(a => a.TestId).SingleAsync(ct);
        var testQuestionCount = await db.QuestionInTests.CountAsync(q => q.TestId == testId, ct);
        var skills = result.Subskills.Select(s => s.SubskillId).ToArray();
        var labels = await db.Subskills.AsNoTracking().Where(s => skills.Contains(s.SubskillId))
            .Select(s => new ReportAreaLabel(s.SubskillId, s.KnowledgeAreaId, s.KnowledgeArea.KnowledgeAreaCode,
                s.KnowledgeArea.KnowledgeAreaName)).ToListAsync(ct);

        // Explicit projections: never load full assignment entities (which contain private teacher-only data).
        var packages = await db.InterventionPackageStudents.AsNoTracking()
            .Where(s => s.StudentId == studentId && s.InterventionPackage.AssessmentId == assessmentId
                && (s.InterventionPackage.Status == "Published" || s.InterventionPackage.Status == "Archived"))
            .OrderBy(s => s.InterventionPackageId)
            .Select(s => new
            {
                PackageId = s.InterventionPackageId, s.InterventionPackage.PackageName,
                s.InterventionPackage.Instructions, s.InterventionPackage.Status, s.DeliveredDate
            }).ToListAsync(ct);
        var packageIds = packages.Select(p => p.PackageId).ToArray();
        var resources = await db.InterventionPackageResources.AsNoTracking()
            .Where(r => packageIds.Contains(r.InterventionPackageId)).OrderBy(r => r.SortOrder)
            .Select(r => new
            {
                PackageId = r.InterventionPackageId,
                Resource = new InterventionResourceRow(r.InterventionResourceId, r.SortOrder,
                    r.InterventionResource.ResourceName, r.InterventionResource.ResourceUrl,
                    r.InterventionResource.Description, r.InterventionResource.SubskillId,
                    r.InterventionResource.Subskill.SubskillCode, r.InterventionResource.Subskill.SubskillName,
                    r.InterventionResource.Status)
            }).ToListAsync(ct);
        var report = new StudentReportViewModel(result.Assessment, result.StudentId, result.StudentCode, result.StudentName,
            result.HasAttempt, result.Score, testQuestionCount, result.TotalResponses, result.CorrectResponses,
            result.IncorrectResponses, result.AccuracyPercentage, BuildAreas(result.Subskills, labels),
            packages.Select(p => new ReportPackage(p.PackageId, p.PackageName, p.Instructions, p.Status, p.DeliveredDate,
                resources.Where(r => r.PackageId == p.PackageId).Select(r => r.Resource).ToArray())).ToArray(),
            DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)));
        if (worksheets?.Value.Enabled == true)
        {
            var counts = await db.InterventionPackageQuestions.AsNoTracking().Where(q => packageIds.Contains(q.InterventionPackageId))
                .GroupBy(q => q.InterventionPackageId).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, ct);
            var titles = await db.InterventionPackageTheories.AsNoTracking().Where(t => packageIds.Contains(t.InterventionPackageId))
                .OrderBy(t => t.SortOrder).Select(t => new { Id = t.InterventionPackageId, t.SnStitle }).ToListAsync(ct);
            report = report with { Packages = report.Packages.Select(p => p with { WorksheetQuestionCount = counts.GetValueOrDefault(p.PackageId),
                WorksheetTheoryTitles = titles.Where(t => t.Id == p.PackageId).Select(t => t.SnStitle).ToArray() }).ToArray() };
        }
        return report;
    }

    internal static IReadOnlyList<ReportKnowledgeArea> BuildAreas(IReadOnlyList<IndividualSubskillRow> skills,
        IReadOnlyList<ReportAreaLabel> labels)
    {
        var lookup = labels.ToDictionary(l => l.SubskillId);
        // Counts/percentages come directly from S1. Area percentage uses the existing weighted helper.
        return skills.GroupBy(s => lookup[s.SubskillId].KnowledgeAreaId).Select(g =>
        {
            var area = lookup[g.First().SubskillId];
            return new ReportKnowledgeArea(area.KnowledgeAreaId, area.Code, area.Name,
                g.Select(s => new ReportSubskill(s.SubskillId, s.SubskillCode, s.SubskillName, s.QuestionCount,
                    s.ResponseCount, s.CorrectCount, s.IncorrectCount, s.AccuracyPercentage)).ToArray());
        }).OrderBy(g => g.Code, StringComparer.Ordinal).ToArray();
    }
}
