namespace Harmonia.Web.Models.Entities;

public sealed class LearningObservation
{
    public long LearningObservationId { get; set; }
    public long StudentId { get; set; }
    public long ClassId { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public string? TaskCompletion { get; set; }
    public string? Participation { get; set; }
    public string? Preparation { get; set; }
    public string? Cooperation { get; set; }
    public short RubricVersion { get; set; } = 1;
    public string CreatedByUserId { get; set; } = "";
    public string? UpdatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long RowVersion { get; set; } = 1;
}
