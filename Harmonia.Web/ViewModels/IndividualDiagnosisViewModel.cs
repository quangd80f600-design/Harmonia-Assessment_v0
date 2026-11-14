namespace Harmonia.Web.ViewModels;

public sealed record IndividualQuestionEvidence(long QuestionInTestId, short QuestionNumber,
    long SubskillId, string SubskillCode, string SubskillName, string QuestionText,
    string OptionA, string OptionB, string OptionC, string OptionD, string AnswerKey, bool? IsCorrect);

public sealed record IndividualSubskillRow(long SubskillId, string SubskillCode, string SubskillName,
    IReadOnlyList<IndividualQuestionEvidence> Questions)
{
    public int QuestionCount => Questions.Count;
    public int ResponseCount => Questions.Count(q => q.IsCorrect.HasValue);
    public int CorrectCount => Questions.Count(q => q.IsCorrect == true);
    public int IncorrectCount => Questions.Count(q => q.IsCorrect == false);
    public decimal? AccuracyPercentage => AnalysisPercentage.Calculate(CorrectCount, ResponseCount);
}

public sealed record IndividualDiagnosisViewModel(AssessmentHeader Assessment, long StudentId,
    string StudentCode, string StudentName, long? AttemptId, decimal? Score,
    IReadOnlyList<IndividualSubskillRow> Subskills)
{
    public long ClassId { get; init; }
    public bool HasAttempt => AttemptId.HasValue;
    public int QuestionCount => Subskills.Sum(s => s.QuestionCount);
    public int TotalResponses => Subskills.Sum(s => s.ResponseCount);
    public int CorrectResponses => Subskills.Sum(s => s.CorrectCount);
    public int IncorrectResponses => Subskills.Sum(s => s.IncorrectCount);
    public decimal? AccuracyPercentage => AnalysisPercentage.Calculate(CorrectResponses, TotalResponses);
}
