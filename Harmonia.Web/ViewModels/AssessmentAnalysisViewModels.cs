namespace Harmonia.Web.ViewModels;

public sealed record AssessmentHeader(long AssessmentId, string AssessmentName,
    DateOnly AssessmentDate, string ClassName, string TestName);

public sealed record SubskillSummaryRow(long SubskillId, string SubskillCode,
    string SubskillName, int QuestionCount, long TotalResponses, long IncorrectResponses)
{
    public long CorrectResponses => TotalResponses - IncorrectResponses;
    public decimal? ErrorPercentage => AnalysisPercentage.Calculate(IncorrectResponses, TotalResponses);
}

public sealed record QuestionAnalysisRow(long QuestionInTestId, short QuestionNumber,
    long SubskillId, string SubskillCode, string SubskillName,
    string QuestionText, string OptionA, string OptionB, string OptionC, string OptionD,
    string AnswerKey, long TotalResponses, long IncorrectResponses)
{
    public long CorrectResponses => TotalResponses - IncorrectResponses;
    public decimal? ErrorPercentage => AnalysisPercentage.Calculate(IncorrectResponses, TotalResponses);
}

public sealed record AssessmentSubskillSummaryViewModel(
    AssessmentHeader Assessment, IReadOnlyList<SubskillSummaryRow> Rows);

public sealed record AssessmentQuestionDetailViewModel(
    AssessmentHeader Assessment, long SubskillId, string SubskillCode,
    string SubskillName, IReadOnlyList<QuestionAnalysisRow> Rows);

internal static class AnalysisPercentage
{
    // Match PostgreSQL ROUND(numeric, 2), including midpoint behavior.
    public static decimal? Calculate(long incorrect, long total) => total == 0
        ? null : Math.Round(100m * incorrect / total, 2, MidpointRounding.AwayFromZero);
}
