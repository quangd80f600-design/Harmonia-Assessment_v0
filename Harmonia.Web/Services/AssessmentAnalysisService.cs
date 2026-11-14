using Harmonia.Web.Data;
using Harmonia.Web.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Harmonia.Web.Services;

public sealed class AssessmentAnalysisService(HarmoniaDbContext db)
{
    public async Task<AssessmentSubskillSummaryViewModel?> GetSummaryAsync(
        long assessmentId, CancellationToken cancellationToken = default)
    {
        var assessment = await GetHeaderAsync(assessmentId, cancellationToken);
        if (assessment is null) return null;

        var questions = await QuestionQuery(assessmentId).ToListAsync(cancellationToken);
        return new(assessment, Summarize(questions));
    }

    public async Task<AssessmentQuestionDetailViewModel?> GetQuestionsAsync(
        long assessmentId, long subskillId, CancellationToken cancellationToken = default)
    {
        var assessment = await GetHeaderAsync(assessmentId, cancellationToken);
        if (assessment is null) return null;

        var questions = await QuestionQuery(assessmentId, subskillId).ToListAsync(cancellationToken);
        // A valid Subskill elsewhere is not necessarily part of this assessment's Test.
        if (questions.Count == 0) return null;
        var first = questions[0];
        return new(assessment, subskillId, first.SubskillCode, first.SubskillName,
            questions.OrderBy(q => q.ErrorPercentage is null)
                .ThenByDescending(q => q.ErrorPercentage)
                .ThenByDescending(q => q.IncorrectResponses)
                .ThenBy(q => q.QuestionNumber).ToList());
    }

    private Task<AssessmentHeader?> GetHeaderAsync(long assessmentId, CancellationToken cancellationToken) =>
        db.Assessments.AsNoTracking().Where(a => a.AssessmentId == assessmentId)
            .Select(a => new AssessmentHeader(a.AssessmentId, a.AssessmentName,
                a.AssessmentDate, a.Class.ClassName, a.Test.TestName))
            .SingleOrDefaultAsync(cancellationToken);

    internal IQueryable<QuestionAnalysisRow> QuestionQuery(long assessmentId, long? subskillId = null)
        => BuildQuestionQuery(db.Assessments.AsNoTracking(), assessmentId, subskillId);

    internal static IQueryable<QuestionAnalysisRow> BuildQuestionQuery(
        IQueryable<Models.Entities.Assessment> assessments, long assessmentId, long? subskillId = null)
    {
        // Start from the Test's snapshots: unanswered questions must remain visible.
        // Counts execute in PostgreSQL; no student records or individual answers are loaded.
        var questions = assessments
            .Where(a => a.AssessmentId == assessmentId)
            .SelectMany(a => a.Test.QuestionInTests)
            .Where(q => subskillId == null || q.SnSsubskillId == subskillId);

        return questions.Select(q => new QuestionAnalysisRow(
            q.QuestionInTestId, q.QuestionNumber, q.SnSsubskillId,
            q.SnSsubskill.SubskillCode, q.SnSsubskill.SubskillName,
            q.SnSquestionText, q.SnSoptionA, q.SnSoptionB, q.SnSoptionC, q.SnSoptionD,
            q.SnSanswerKey,
            q.Responses.LongCount(r => r.Attempt.AssessmentId == assessmentId),
            q.Responses.LongCount(r => r.Attempt.AssessmentId == assessmentId && !r.IsCorrect)));
    }

    internal static IReadOnlyList<SubskillSummaryRow> Summarize(IEnumerable<QuestionAnalysisRow> questions) =>
        questions.GroupBy(q => new { q.SubskillId, q.SubskillCode, q.SubskillName })
            .Select(g => new SubskillSummaryRow(g.Key.SubskillId, g.Key.SubskillCode,
                g.Key.SubskillName, g.Count(), g.Sum(q => q.TotalResponses),
                g.Sum(q => q.IncorrectResponses)))
            .OrderBy(r => r.ErrorPercentage is null)
            .ThenByDescending(r => r.ErrorPercentage)
            .ThenByDescending(r => r.IncorrectResponses)
            .ThenBy(r => r.SubskillCode, StringComparer.Ordinal).ToList();
}
