namespace Harmonia.Web.ViewModels;

public sealed class SaveParentCommentInput
{
    public long? DraftId { get; set; }
    public long? ExpectedVersion { get; set; }
    public long? ExpectedApprovedId { get; set; }
    public long? ObservationId { get; set; }
    public string? Text { get; set; } = "";
    public string? ProcessText { get; set; } = "";
}
public sealed class ParentCommentActionInput
{
    public long DraftId { get; set; }
    public long ExpectedVersion { get; set; }
    public bool Confirmed { get; set; }
}
public sealed class InlineObservationInput
{
    public DateOnly? PeriodStart { get; set; }
    public DateOnly? PeriodEnd { get; set; }
    public string? TaskCompletion { get; set; }
    public string? Participation { get; set; }
    public string? Preparation { get; set; }
    public string? Cooperation { get; set; }
    public SaveParentCommentInput Draft { get; set; } = new();
}
public sealed record InlineObservationResult(ParentCommentEditor? Editor,ParentCommentWriteResult Result);
public sealed record CommentObservationChoice(long Id, DateOnly Start, DateOnly End, string Author,
    string? Task, string? Participation, string? Preparation, string? Cooperation);
public sealed record CommentRevision(long Id, int Revision, string Status, string Text, long? ObservationId,
    long Version, string Author, bool CanEdit, bool Stale, DateTime? ApprovedAt)
{
    public string Source { get; init; } = "Manual";
    public string? ProcessText { get; init; }
    public bool SectionsSeparated { get; init; }
}
public sealed record ParentCommentEditor(StudentReportViewModel Report, bool CanWrite,
    IReadOnlyList<CommentObservationChoice> Observations, CommentRevision? Draft, CommentRevision? Approved,
    IReadOnlyList<CommentRevision> History, SaveParentCommentInput Input, IReadOnlyList<string> Errors);
public sealed record ParentCommentWriteResult(bool Succeeded, string Message, bool NotFound = false, bool Conflict = false);

// Only these public fields may enter the parent-facing report. No raw evidence, actors, draft or AI metadata.
public sealed record ParentReportObservation(DateOnly Start, DateOnly End, string Task, string Participation,
    string Preparation, string Cooperation);
public sealed record ParentReportComment(string Text, ParentReportObservation? Observation)
{ public string? ProcessText { get; init; } }
