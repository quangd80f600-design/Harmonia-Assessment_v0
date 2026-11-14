using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Harmonia.Web.Data;
using Harmonia.Web.Models.Entities;
using Harmonia.Web.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Harmonia.Web.Services;

public sealed class ParentCommentOptions { public bool Enabled { get; set; } public bool SeparateSections { get; set; } }
public sealed record CommentSkillEvidence(long Id, string Code, string Name, int Questions, int Responses, int Correct, string? Percentage);
public sealed record CommentPlanEvidence(long Id, string Name, string? Instructions, string Status, DateOnly? Delivered,
    int WorksheetQuestions, string[] TheoryTitles, string[] Resources);
public sealed record CommentObservationEvidence(long Id, DateOnly Start, DateOnly End, short Rubric,
    string? Task, string? Participation, string? Preparation, string? Cooperation);
public sealed record CommentSemanticEvidence(short SchemaVersion, long AssessmentId, long StudentId,
    DateOnly AssessmentDate, bool HasAttempt, string? Score, int Questions, int Responses, int Correct,
    CommentSkillEvidence[] Skills, CommentPlanEvidence[] Plans, CommentObservationEvidence? Observation);
public sealed record CommentEvidenceSnapshot(CommentSemanticEvidence Data, long? ObservationRowVersion);

public static class ParentCommentEvidence
{
    public static string? Number(decimal? value) => value?.ToString("0.############################", CultureInfo.InvariantCulture);
    public static string Fingerprint(CommentSemanticEvidence data) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data)))).ToLowerInvariant();
    public static string Serialize(CommentEvidenceSnapshot snapshot) => JsonSerializer.Serialize(snapshot);
    public static string Choice(string? value, IReadOnlyList<ObservationChoice> choices) => choices.FirstOrDefault(c => c.Code == value)?.Label ?? "Chưa đủ thông tin";

    public static async Task<LearningObservation[]> EligibleObservationsAsync(HarmoniaDbContext db, long classId, long studentId, CancellationToken ct)
    {
        var observations = await db.LearningObservations.AsNoTracking().Where(o => o.ClassId == classId && o.StudentId == studentId
            && o.PeriodEnd <= AcademicManagementService.Today && o.RubricVersion == 1)
            .OrderByDescending(o => o.PeriodEnd).ThenByDescending(o => o.LearningObservationId).ToArrayAsync(ct);
        var intervals = await db.ClassMemberships.AsNoTracking().Where(m => m.ClassId == classId && m.StudentId == studentId)
            .Select(m => new ObservationInterval(m.StartDate, m.EndDate)).ToListAsync(ct);
        return observations.Where(o => ObservationRubric.Covers(o.PeriodStart, o.PeriodEnd, intervals)).ToArray();
    }

    public static async Task<CommentEvidenceSnapshot?> CaptureAsync(HarmoniaDbContext db, StudentReportViewModel report,
        long? observationId, CancellationToken ct)
    {
        if (observationId is <= 0) return null;
        LearningObservation? observation = null;
        if (observationId.HasValue)
        {
            var classId = await db.Assessments.Where(a => a.AssessmentId == report.Assessment.AssessmentId).Select(a => a.ClassId).SingleAsync(ct);
            observation = (await EligibleObservationsAsync(db, classId, report.StudentId, ct)).SingleOrDefault(o => o.LearningObservationId == observationId);
            if (observation is null) return null;
        }
        var skills = report.KnowledgeAreas.SelectMany(a => a.Subskills).OrderBy(s => s.SubskillId)
            .Select(s => new CommentSkillEvidence(s.SubskillId, s.Code, s.Name, s.QuestionCount, s.ResponseCount, s.CorrectCount, Number(s.AccuracyPercentage))).ToArray();
        var plans = report.Packages.OrderBy(p => p.PackageId).Select(p => new CommentPlanEvidence(p.PackageId, p.PackageName,
            p.Instructions, p.Status, p.DeliveredDate, p.WorksheetQuestionCount, p.WorksheetTheoryTitles.Order(StringComparer.Ordinal).ToArray(),
            p.Resources.OrderBy(r => r.ResourceId).Select(r => JsonSerializer.Serialize(new { r.ResourceId, r.ResourceName, r.SubskillCode, r.SubskillName, r.Description, r.SafeUrl, r.Status })).ToArray())).ToArray();
        var obs = observation is null ? null : new CommentObservationEvidence(observation.LearningObservationId, observation.PeriodStart,
            observation.PeriodEnd, observation.RubricVersion, observation.TaskCompletion, observation.Participation, observation.Preparation, observation.Cooperation);
        return new(new(1, report.Assessment.AssessmentId, report.StudentId, report.Assessment.AssessmentDate,
            report.HasAttempt, Number(report.Score), report.TestQuestionCount, report.TotalResponses, report.CorrectResponses, skills, plans, obs), observation?.RowVersion);
    }

    public static async Task<ParentReportComment?> ReadApprovedAsync(HarmoniaDbContext db, StudentReportViewModel report, CancellationToken ct, bool separateSections = false)
    {
        // Projection intentionally excludes AiDraftText, raw snapshot and actor IDs from the report path.
        var approved = await db.StudentReportComments.AsNoTracking().Where(c => c.StudentId == report.StudentId
            && c.AssessmentId == report.Assessment.AssessmentId && c.Status == "Approved")
            .Select(c => new { c.StudentReportCommentId, c.CommentText, c.LearningObservationId, c.InputFingerprint, c.InputSchemaVersion }).SingleOrDefaultAsync(ct);
        if (approved is null || approved.InputSchemaVersion != 1) return null;
        var current = await CaptureAsync(db, report, approved.LearningObservationId, ct);
        if (current is null || Fingerprint(current.Data) != approved.InputFingerprint) return null;
        var process = separateSections ? await CommentSectionStorage.ReadPublicAsync(db,approved.StudentReportCommentId,ct) : null;
        if(separateSections && process is null)return null; // Legacy combined text needs teacher review, never guessed/split.
        var o = current.Data.Observation;
        return new(approved.CommentText, o is null ? null : new(o.Start, o.End, Choice(o.Task, ObservationRubric.Task),
            Choice(o.Participation, ObservationRubric.Participation), Choice(o.Preparation, ObservationRubric.Preparation), Choice(o.Cooperation, ObservationRubric.Cooperation))) {ProcessText=process};
    }
}
