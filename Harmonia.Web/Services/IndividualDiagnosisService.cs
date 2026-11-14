using Harmonia.Web.Data;
using Harmonia.Web.Models.Entities;
using Harmonia.Web.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Harmonia.Web.Services;

public sealed class IndividualDiagnosisService(HarmoniaDbContext db)
{
    public async Task<IndividualDiagnosisViewModel?> GetDiagnosisAsync(long assessmentId, long studentId,
        CancellationToken cancellationToken = default)
    {
        var assessment = await BuildAssessmentQuery(db.Assessments.AsNoTracking(), assessmentId)
            .SingleOrDefaultAsync(cancellationToken);
        if (assessment is null) return null;

        // Reuse S4's temporal membership/optional Attempt rules; never trust a URL ID alone.
        var student = await AssessmentParticipationService.BuildStudentQuery(
            db.Students.AsNoTracking().Where(s => s.StudentId == studentId),
            db.Attempts.AsNoTracking(), db.ImportBatches.AsNoTracking(),
            assessmentId, assessment.ClassId, assessment.Header.AssessmentDate)
            .SingleOrDefaultAsync(cancellationToken);
        if (student is null) return null;

        var evidence = student.AttemptId is long attemptId
            ? await BuildEvidenceQuery(db.QuestionInTests.AsNoTracking(), db.Responses.AsNoTracking(),
                assessment.TestId, attemptId).ToListAsync(cancellationToken)
            : new List<IndividualQuestionEvidence>();
        return CreateReport(assessment.Header, student, evidence) with { ClassId = assessment.ClassId };
    }

    internal static IQueryable<DiagnosisAssessmentContext> BuildAssessmentQuery(
        IQueryable<Assessment> assessments, long assessmentId) =>
        assessments.Where(a => a.AssessmentId == assessmentId)
            .Select(a => new DiagnosisAssessmentContext(a.ClassId, a.TestId,
                new AssessmentHeader(a.AssessmentId, a.AssessmentName, a.AssessmentDate,
                    a.Class.ClassName, a.Test.TestName)));

    internal static IQueryable<IndividualQuestionEvidence> BuildEvidenceQuery(
        IQueryable<QuestionInTest> questions, IQueryable<Response> responses, long testId, long attemptId) =>
        from q in questions.Where(q => q.TestId == testId)
        join r in responses.Where(r => r.AttemptId == attemptId)
            on q.QuestionInTestId equals r.QuestionInTestId into answers
        from r in answers.DefaultIfEmpty()
        orderby q.QuestionNumber
        select new IndividualQuestionEvidence(q.QuestionInTestId, q.QuestionNumber,
            q.SnSsubskillId, q.SnSsubskill.SubskillCode, q.SnSsubskill.SubskillName,
            q.SnSquestionText, q.SnSoptionA, q.SnSoptionB, q.SnSoptionC, q.SnSoptionD,
            q.SnSanswerKey, r == null ? null : r.IsCorrect);

    internal static IndividualDiagnosisViewModel CreateReport(AssessmentHeader assessment,
        ParticipationStudentRow student, IEnumerable<IndividualQuestionEvidence> evidence)
    {
        var subskills = student.HasResult
            ? evidence.GroupBy(q => new { q.SubskillId, q.SubskillCode, q.SubskillName })
                .Select(g => new IndividualSubskillRow(g.Key.SubskillId, g.Key.SubskillCode, g.Key.SubskillName,
                    g.OrderBy(q => q.QuestionNumber).ToList()))
                .OrderBy(s => s.AccuracyPercentage is null)
                .ThenBy(s => s.AccuracyPercentage)
                .ThenBy(s => s.SubskillCode, StringComparer.Ordinal).ToList()
            : new List<IndividualSubskillRow>();
        return new(assessment, student.StudentId, student.StudentCode, student.FullName,
            student.AttemptId, student.Score, subskills);
    }
}

internal sealed record DiagnosisAssessmentContext(long ClassId, long TestId, AssessmentHeader Header);
