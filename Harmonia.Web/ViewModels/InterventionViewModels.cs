using System.ComponentModel.DataAnnotations;

namespace Harmonia.Web.ViewModels;

public sealed class CreateInterventionInput
{
    [Range(1, long.MaxValue, ErrorMessage = "Chọn đợt đánh giá nguồn.")]
    public long AssessmentId { get; set; }
    [Required(ErrorMessage = "Nhập tên gói can thiệp."), StringLength(200, ErrorMessage = "Tên gói tối đa 200 ký tự.")]
    public string PackageName { get; set; } = "";
    [StringLength(10000, ErrorMessage = "Hướng dẫn tối đa 10.000 ký tự.")]
    public string? Instructions { get; set; }
}

public sealed class InterventionSelectionInput
{
    [Required, MinLength(1, ErrorMessage = "Chọn ít nhất một mục."), MaxLength(200, ErrorMessage = "Mỗi lần chọn tối đa 200 mục.")]
    public long[] Ids { get; set; } = [];
}

public sealed class InterventionDeliveryInput
{
    public DateOnly? DeliveredDate { get; set; }
    [StringLength(4000, ErrorMessage = "Ghi chú tối đa 4.000 ký tự.")]
    public string? TeacherNote { get; set; }
    [Required, StringLength(64, MinimumLength = 64)]
    public string ExpectedVersion { get; set; } = "";
}

public sealed record InterventionAssessment(long AssessmentId, string AssessmentName, long ClassId,
    string ClassName, DateOnly AssessmentDate, string Status);
public sealed record InterventionListRow(long PackageId, string PackageName, string Status,
    InterventionAssessment Assessment, int ResourceCount, int StudentCount)
{
    public int WorksheetQuestionCount { get; init; }
    public int WorksheetTheoryCount { get; init; }
}
public sealed record InterventionIndexViewModel(bool CanWrite, IReadOnlyList<InterventionListRow> Packages);
public sealed record InterventionCreateViewModel(CreateInterventionInput Input, IReadOnlyList<InterventionAssessment> Assessments);
public sealed record InterventionResourceRow(long ResourceId, short SortOrder, string ResourceName,
    string ResourceUrl, string Description, long SubskillId, string SubskillCode, string SubskillName, string Status)
{
    public string? SafeUrl => Uri.TryCreate(ResourceUrl, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) && string.IsNullOrEmpty(uri.UserInfo)
        ? uri.AbsoluteUri : null;
}
public sealed record InterventionCandidate(long StudentId, string StudentCode, string FullName);
public sealed record InterventionAssignment(long AssignmentId, long StudentId, string StudentCode,
    string FullName, DateOnly? DeliveredDate, string? TeacherNote, string Version);
public sealed record InterventionDetailsViewModel(long PackageId, string PackageName, string? Instructions,
    string Status, InterventionAssessment Assessment, bool CanWrite, IReadOnlyList<InterventionResourceRow> Resources,
    IReadOnlyList<InterventionResourceRow> AvailableResources, IReadOnlyList<InterventionCandidate> Candidates,
    IReadOnlyList<InterventionAssignment> Assignments)
{
    public bool HasInternalWorksheet { get; init; }
    public bool CanAttach => CanWrite && Status == "Draft" && Assignments.Count == 0 && !HasInternalWorksheet;
    public bool CanAssign => CanWrite && (Status is "Draft" or "Published") && (Resources.Count > 0 || HasInternalWorksheet)
        && Resources.All(r => r.Status == "Active" && r.SafeUrl is not null);
}
public sealed record InterventionWriteResult(bool Succeeded, string Message, long? PackageId = null, bool NotFound = false);
