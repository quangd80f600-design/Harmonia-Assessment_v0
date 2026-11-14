namespace Harmonia.Web.Models.Entities;

public sealed class StudentReportComment
{
    public long StudentReportCommentId { get; set; }
    public long StudentId { get; set; }
    public long AssessmentId { get; set; }
    public long? LearningObservationId { get; set; }
    public long? BaseApprovedCommentId { get; set; }
    public int RevisionNumber { get; set; }
    public string Status { get; set; } = "Draft";
    public string CommentText { get; set; } = "";
    public string Source { get; set; } = "Manual";
    public string? AiDraftText { get; set; }
    public string? AiModel { get; set; }
    public string? PromptVersion { get; set; }
    public DateTime? GeneratedAt { get; set; }
    public short InputSchemaVersion { get; set; } = 1;
    public string InputSnapshot { get; set; } = "{}";
    public string InputFingerprint { get; set; } = "";
    public string CreatedByUserId { get; set; } = "";
    public string? UpdatedByUserId { get; set; }
    public string? ApprovedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public long RowVersion { get; set; } = 1;
}
