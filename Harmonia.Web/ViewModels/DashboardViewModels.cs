namespace Harmonia.Web.ViewModels;

public sealed record DashboardFilter(long? ClassId = null, long? AssessmentId = null);
public sealed record DashboardOption(long Id, string Name);
public sealed record DashboardClassRow(long Id, string Code, string Name, short Grade, string AcademicYear, string Status, int Students);
public sealed record DashboardAssessmentContext(long Id, long ClassId, string Name, string ClassName, DateOnly Date, string Status);
public sealed record DashboardAssessmentRow(DashboardAssessmentContext Assessment, ResultCompletion? Completion, bool CanImport);
public sealed record DashboardPackageRow(long Id, string Name, string Status, long BaselineId, string BaselineName, int AssignedCount, int MissingDeliveryDateCount);
public sealed record DashboardPackageLink(long Id, string Name);
public sealed record DashboardFollowupEvent(long BaselineId, string BaselineName, long FollowupId, string FollowupName,
    string ClassName, DateOnly Date, bool HasAnyAttempt, IReadOnlyList<DashboardPackageLink> Packages, int PackageCount);
public sealed record DashboardPage(DateOnly Today, bool IsManager, DashboardFilter Filter,
    IReadOnlyList<DashboardOption> Classes, IReadOnlyList<DashboardAssessmentContext> AssessmentOptions,
    IReadOnlyList<DashboardClassRow> ClassRows, int? UnassignedClasses,
    IReadOnlyList<DashboardAssessmentRow> RecentAssessments, DashboardAssessmentContext? SelectedAssessment,
    IReadOnlyList<SubskillSummaryRow> SkillRows, bool HasResponses,
    IReadOnlyList<DashboardPackageRow> Packages, IReadOnlyList<DashboardFollowupEvent> Followups,
    bool InterventionsEnabled, bool CanCreateClass, AssessmentCreationGuidanceViewModel? Creation, bool SelectedCanImport)
{
    public IReadOnlyList<DashboardPlannedFollowup> PlannedFollowups { get; init; } = [];
}

public static class DashboardText
{
    public static string Completion(ResultCompletion? completion) => completion switch
    {
        null => "Chưa xác định được tiến độ kết quả.",
        { HasStudents: false } => "Chưa có học sinh thuộc lớp vào ngày đánh giá.",
        { RecordedStudents: 0 } => "Chưa có kết quả.",
        { IsComplete: true } => "Đã có kết quả cho toàn bộ học sinh trong danh sách.",
        _ => $"Còn {completion.MissingStudents} học sinh chưa có kết quả."
    };
    public static string Status(string status) => status switch
    {
        "Draft" => "Bản nháp", "Open" => "Đang mở", "Completed" => "Đã hoàn tất",
        "Cancelled" => "Đã hủy", "Published" => "Đã phát hành", "Archived" => "Đã lưu trữ",
        "Active" => "Đang hoạt động", "Inactive" => "Ngừng hoạt động", _ => status
    };
    public static string Followup(DashboardFollowupEvent item, DateOnly today) => item.HasAnyAttempt
        ? "Đã có kết quả" : item.Date < today ? "Ngày đánh giá đã qua; chưa ghi nhận kết quả" : "Sắp tới";
}
