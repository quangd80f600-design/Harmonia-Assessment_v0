namespace Harmonia.Web.ViewModels;

public sealed record ImportError(int? Line, string Field, string Message);
public sealed record ImportPageViewModel(AssessmentHeader Assessment, int QuestionCount,
    string DemoActor, string? BlockReason, IReadOnlyList<ImportError> Errors)
{
    public bool CanImport => BlockReason is null;
    public ResultCompletion? ResultCompletion { get; init; }
}
public sealed record ImportStudentPreview(string StudentCode, string StudentName,
    int QuestionCount, int Correct, int Incorrect, int Blank, decimal Score);
public sealed record ImportPreviewViewModel(ImportPageViewModel Page, string FileName, string Token,
    DateTimeOffset ExpiresAt, IReadOnlyList<ImportStudentPreview> Students)
{
    public int TotalBlank => Students.Sum(s => s.Blank);
    public int StudentsWithBlank => Students.Count(s => s.Blank > 0);
}
public sealed record CsvTemplateDownload(byte[] Content, string FileName);
public sealed record ImportPreparation(ImportPageViewModel Page, ImportPreviewViewModel? Preview);
public sealed record ImportOutcome(AssessmentHeader Assessment, bool Succeeded, long? BatchId,
    int StudentCount, int ResponseCount, IReadOnlyList<ImportError> Errors, bool Uncertain = false);
