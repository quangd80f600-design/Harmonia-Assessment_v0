using System.ComponentModel.DataAnnotations;

namespace Harmonia.Web.ViewModels;

public sealed class AssessmentFollowupPlanInput
{
    public DateOnly? PlannedFollowupDate { get; set; }
    [Required(ErrorMessage = "Phiên chỉnh sửa chưa hợp lệ. Hãy mở lại biểu mẫu.")]
    [StringLength(64, MinimumLength = 64)]
    public string ExpectedVersion { get; set; } = "";
    public string Operation { get; set; } = "Save";
    public bool ConfirmClear { get; set; }
}

public sealed record AssessmentFollowupPlanSummary(long AssessmentId, string AssessmentName,
    string ClassName, DateOnly AssessmentDate, DateOnly? PlannedFollowupDate, bool CanEdit);
public sealed record AssessmentFollowupPlanPage(AssessmentFollowupPlanSummary Summary, AssessmentFollowupPlanInput Input);
public sealed record AssessmentFollowupPlanResult(bool Succeeded, string Message, bool NotFound = false, bool Conflict = false, bool Uncertain = false);
public sealed record DashboardPlannedFollowup(long AssessmentId, string AssessmentName, string ClassName,
    DateOnly AssessmentDate, DateOnly PlannedDate, bool CanEdit);

public static class AssessmentFollowupPlanRules
{
    public static string? ValidateDate(DateOnly assessmentDate, DateOnly? plannedDate) =>
        plannedDate.HasValue && plannedDate.Value <= assessmentDate
            ? "Ngày dự kiến đánh giá lại phải sau ngày đánh giá ban đầu." : null;
    public static string Label(DateOnly plannedDate, DateOnly today) => plannedDate == today
        ? "Đến ngày dự kiến đánh giá lại" : plannedDate < today
        ? "Đã qua ngày dự kiến đánh giá lại" : "Dự kiến đánh giá lại";
}
