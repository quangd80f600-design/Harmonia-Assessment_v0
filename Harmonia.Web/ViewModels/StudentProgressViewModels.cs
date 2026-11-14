namespace Harmonia.Web.ViewModels;

public sealed record ProgressSubskill(long Id, string Code, string Name);

public sealed record ProgressMeasure(bool HasAttempt, int QuestionCount, int ResponseCount, int CorrectCount)
{
    public int IncorrectCount => ResponseCount - CorrectCount;
    public decimal? AccuracyPercentage => HasAttempt && QuestionCount > 0
        ? AnalysisPercentage.Calculate(CorrectCount, ResponseCount) : null;
    public bool HasLimitedEvidence => ResponseCount is > 0 and < 5;
    public string Status => QuestionCount == 0 ? "Không có nội dung này trong đề"
        : !HasAttempt ? "Chưa có kết quả"
        : ResponseCount == 0 ? "Chưa có dữ liệu trả lời"
        : ResponseCount < QuestionCount ? $"Đã ghi nhận {ResponseCount}/{QuestionCount} câu"
        : "Đã ghi nhận đủ câu trả lời";
}

public sealed record ProgressAssessment(long Id, string Name, DateOnly Date, string TestName,
    decimal? Score, ProgressMeasure Overall, ProgressMeasure? SelectedSkill);

public sealed record StudentProgressViewModel(long ClassId, string ClassName, long StudentId,
    string StudentCode, string StudentName, IReadOnlyList<ProgressSubskill> Subskills,
    long? SubskillId, int Page, int TotalAssessments, IReadOnlyList<ProgressAssessment> Assessments)
{
    public const int PageSize = 20;
    public int TotalPages => (int)(((long)TotalAssessments + PageSize - 1) / PageSize);
    public ProgressSubskill? SelectedSubskill => Subskills.FirstOrDefault(s => s.Id == SubskillId);
}
