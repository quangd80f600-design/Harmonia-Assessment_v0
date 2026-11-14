namespace Harmonia.Web.ViewModels;

public sealed record ReportSubskill(long SubskillId, string Code, string Name, int QuestionCount,
    int ResponseCount, int CorrectCount, int IncorrectCount, decimal? AccuracyPercentage)
{
    // Display caveat only, not a statistical confidence rule or an ability classification.
    public bool HasLimitedEvidence => ResponseCount is > 0 and < 5;
    public int UnrecordedCount => QuestionCount - ResponseCount;
}

public sealed record ReportKnowledgeArea(long KnowledgeAreaId, string Code, string Name,
    IReadOnlyList<ReportSubskill> Subskills)
{
    public int QuestionCount => Subskills.Sum(s => s.QuestionCount);
    public int ResponseCount => Subskills.Sum(s => s.ResponseCount);
    public int CorrectCount => Subskills.Sum(s => s.CorrectCount);
    public int IncorrectCount => Subskills.Sum(s => s.IncorrectCount);
    public decimal? AccuracyPercentage => AnalysisPercentage.Calculate(CorrectCount, ResponseCount);
}

public sealed record ReportPackage(long PackageId, string PackageName, string? Instructions,
    string Status, DateOnly? DeliveredDate, IReadOnlyList<InterventionResourceRow> Resources)
{
    public int WorksheetQuestionCount { get; init; }
    public IReadOnlyList<string> WorksheetTheoryTitles { get; init; } = [];
}

// Deliberately contains only data allowed in the parent-facing report. No EF entities or internal notes.
public sealed record StudentReportViewModel(AssessmentHeader Assessment, long StudentId, string StudentCode,
    string StudentName, bool HasAttempt, decimal? Score, int TestQuestionCount, int TotalResponses,
    int CorrectResponses, int IncorrectResponses, decimal? AccuracyPercentage,
    IReadOnlyList<ReportKnowledgeArea> KnowledgeAreas, IReadOnlyList<ReportPackage> Packages,
    DateTimeOffset GeneratedAt)
{
    public ParentReportComment? ApprovedComment { get; init; }
}

internal sealed record ReportAreaLabel(long SubskillId, long KnowledgeAreaId, string Code, string Name);
