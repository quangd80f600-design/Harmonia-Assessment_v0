namespace Harmonia.Web.ViewModels;

public sealed record QuestionCsvRow(int Line, string QuestionCode, string SubskillCode, string QuestionText,
    string OptionA, string OptionB, string OptionC, string OptionD, string AnswerKey)
{
    public QuestionInput ToInput(long subskillId) => new()
    {
        QuestionCode = QuestionCode, SubskillId = subskillId, QuestionText = QuestionText,
        OptionA = OptionA, OptionB = OptionB, OptionC = OptionC, OptionD = OptionD, AnswerKey = AnswerKey,
        DifficultyLevel = 1, Explanation = ""
    };
}
public sealed record ParsedQuestions(IReadOnlyList<QuestionCsvRow> Rows, IReadOnlyList<ImportError> Errors);
public sealed record QuestionCsvSkill(long Id, string Code, string Name, long AreaId, string AreaName,
    long UnitId, string UnitName, short Grade);
public sealed record QuestionCsvPreviewRow(QuestionCsvRow Question, QuestionCsvSkill Skill);
public sealed record QuestionCsvPage(IReadOnlyList<QuestionCsvSkill> Skills, IReadOnlyList<ImportError> Errors)
{
    public string UsagePurpose { get; init; } = "ForTest";
}
public sealed record QuestionCsvPreview(string Token, DateTimeOffset ExpiresAt, IReadOnlyList<QuestionCsvPreviewRow> Rows)
{
    public string UsagePurpose { get; init; } = "ForTest";
}
public sealed record QuestionCsvPreparation(QuestionCsvPreview? Preview, IReadOnlyList<ImportError> Errors);
public sealed record QuestionCsvOutcome(bool Succeeded, int Count, string Message, bool CommitUncertain = false);
