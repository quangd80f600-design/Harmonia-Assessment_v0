using System.ComponentModel.DataAnnotations;
namespace Harmonia.Web.ViewModels;

public sealed class LinkFollowupInput
{
    [Range(1, long.MaxValue, ErrorMessage = "Chọn đợt đánh giá theo dõi hợp lệ.")]
    public long AssessmentId { get; set; }
}
public sealed record FollowupAssessment(long Id, string Name, DateOnly Date, string Status, long ClassId, long TestId);
public sealed record FollowupSkill(long Id, string Code, string Name);
public sealed record FollowupMeasure(int QuestionCount, bool InRoster, bool HasAttempt, int ResponseCount, int CorrectCount)
{
    public decimal? Percentage => InRoster && HasAttempt && QuestionCount > 0
        ? AnalysisPercentage.Calculate(CorrectCount, ResponseCount) : null;
    public string? MissingReason => !InRoster ? "Không thuộc lớp tại ngày đánh giá"
        : QuestionCount == 0 ? "Không có câu hỏi cùng Subskill_ID"
        : !HasAttempt ? "Chưa ghi nhận kết quả đánh giá"
        : ResponseCount == 0 ? "Missing — Chưa có Response cho kỹ năng này" : null;
}
public sealed record FollowupComparison(FollowupAssessment Assessment, FollowupMeasure Result, DateOnly? DeliveredDate,
    FollowupMeasure Baseline)
{
    public decimal? DeltaPoints => Baseline.Percentage.HasValue && Result.Percentage.HasValue
        ? Math.Round(100m * ((decimal)Result.CorrectCount / Result.ResponseCount
            - (decimal)Baseline.CorrectCount / Baseline.ResponseCount), 2, MidpointRounding.AwayFromZero) : null;
    public string DeliveryMessage => DeliveredDate is null ? "Chưa ghi nhận ngày phát học liệu"
        : DeliveredDate > Assessment.Date ? "Ngày phát học liệu muộn hơn ngày đánh giá"
        : "Đã ghi nhận phát học liệu trước hoặc vào ngày đánh giá";
}
public sealed record FollowupStudentSkill(long StudentId, string StudentCode, string StudentName, FollowupSkill Skill,
    FollowupMeasure Baseline, IReadOnlyList<FollowupComparison> Followups);
public sealed record InterventionFollowupPage(long PackageId, string PackageName, string PackageStatus,
    FollowupAssessment Baseline, bool CanLink, IReadOnlyList<FollowupAssessment> Candidates,
    IReadOnlyList<FollowupAssessment> Followups, IReadOnlyList<FollowupStudentSkill> Rows,
    int AssignedStudentCount, int TargetSkillCount);
public sealed record FollowupWriteResult(bool Succeeded, string Message, bool NotFound = false);
