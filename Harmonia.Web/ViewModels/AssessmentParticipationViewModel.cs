namespace Harmonia.Web.ViewModels;

public enum ParticipationStatus { All, Missing, Recorded }

// Computed result coverage, independent of the Assessment's organizational Status.
public sealed record ResultCompletion(int ExpectedStudents, int RecordedStudents)
{
    public int MissingStudents => ExpectedStudents - RecordedStudents;
    public bool HasStudents => ExpectedStudents > 0;
    public bool IsComplete => HasStudents && MissingStudents == 0;
    public bool HasMissingResults => MissingStudents > 0;
}

public sealed record ParticipationStudentRow(long StudentId, string StudentCode, string FullName,
    long? AttemptId, decimal? Score, long? ImportBatchId, string? ImportFileName, string? ImportStatus)
{
    public bool HasResult => AttemptId.HasValue;
}

public sealed record AssessmentParticipationViewModel(AssessmentHeader Assessment,
    IReadOnlyList<ParticipationStudentRow> Students, ParticipationStatus StatusFilter = ParticipationStatus.All)
{
    public AssessmentFollowupPlanSummary? FollowupPlan { get; init; }

    public int ExpectedStudents => Students.Count;
    public int RecordedStudents => Students.Count(s => s.HasResult);
    public int MissingStudents => ExpectedStudents - RecordedStudents;
    public decimal? RecordingPercentage => ExpectedStudents == 0 ? null
        : Math.Round(100m * RecordedStudents / ExpectedStudents, 2, MidpointRounding.AwayFromZero);

    // Filter the table only. KPI denominators always refer to the full eligible roster.
    public IEnumerable<ParticipationStudentRow> VisibleStudents => Students.Where(s => StatusFilter switch
    {
        ParticipationStatus.Missing => !s.HasResult,
        ParticipationStatus.Recorded => s.HasResult,
        _ => true
    });
}
