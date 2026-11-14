namespace Harmonia.Web.ViewModels;

public sealed record ObservationChoice(string Code, string Label);
public sealed record ObservationInterval(DateOnly Start, DateOnly? End);

public static class ObservationRubric
{
    public static IReadOnlyList<ObservationChoice> Task { get; } = [new("Full", "Đầy đủ"), new("Partial", "Một phần"), new("NotCompleted", "Chưa hoàn thành")];
    public static IReadOnlyList<ObservationChoice> Participation { get; } = [new("Proactive", "Chủ động"), new("WithPrompt", "Khi được hướng dẫn"), new("Limited", "Còn hạn chế")];
    public static IReadOnlyList<ObservationChoice> Preparation { get; } = [new("Full", "Đầy đủ"), new("Partial", "Một phần"), new("NotPrepared", "Chưa chuẩn bị")];
    public static IReadOnlyList<ObservationChoice> Cooperation { get; } = [new("Positive", "Phối hợp được"), new("NeedsSupport", "Cần hướng dẫn")];
    public static bool Valid(string? value, IReadOnlyList<ObservationChoice> choices) => value is null || choices.Any(c => c.Code == value);
    public static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // Membership/assignment intervals are half-open, while the visible observation end is inclusive.
    public static bool Covers(DateOnly start, DateOnly end, IEnumerable<ObservationInterval> intervals)
    {
        if (end < start) return false;
        var cursor = start.DayNumber;
        foreach (var i in intervals.OrderBy(i => i.Start))
        {
            var stop = i.End?.DayNumber ?? int.MaxValue;
            if (stop <= cursor) continue;
            if (i.Start.DayNumber > cursor) return false;
            cursor = stop;
            if (cursor > end.DayNumber) return true;
        }
        return false;
    }
}

public sealed class ObservationInputRow
{
    public bool Selected { get; set; }
    public long StudentId { get; set; }
    public long? ObservationId { get; set; }
    public long? ExpectedVersion { get; set; }
    public string? TaskCompletion { get; set; }
    public string? Participation { get; set; }
    public string? Preparation { get; set; }
    public string? Cooperation { get; set; }
}

public sealed class ObservationSaveInput
{
    public DateOnly? PeriodStart { get; set; }
    public DateOnly? PeriodEnd { get; set; }
    public string? AuthorId { get; set; } // Return context only; never used as the creator of a new row.
    public int Page { get; set; } = 1;
    public List<ObservationInputRow> Rows { get; set; } = [];
}

public sealed record ObservationClass(long Id, string Name);
public sealed record ObservationAuthor(string Id, string Name);
public sealed record ObservationSelect(string Name, string? Value, IReadOnlyList<ObservationChoice> Choices, bool Enabled, string Label);
public sealed record ObservationStudent(long StudentId, string Code, string Name, bool FullMembership,
    bool CanEdit, ObservationInputRow Input, DateTime? UpdatedAt);
public sealed record LearningObservationPage(IReadOnlyList<ObservationClass> Classes, long? ClassId,
    DateOnly PeriodStart, DateOnly PeriodEnd, IReadOnlyList<ObservationAuthor> Authors, string AuthorId,
    IReadOnlyList<ObservationStudent> Students, int Page, int StudentCount, bool AssignmentCoversPeriod,
    IReadOnlyList<string> Errors)
{
    public const int PageSize = 40;
    public int TotalPages => (int)(((long)StudentCount + PageSize - 1) / PageSize);
}
public sealed record ObservationSaveResult(bool Saved, string Message, bool NotFound = false,
    bool Conflict = false, int Created = 0, int Updated = 0, int Unchanged = 0);
