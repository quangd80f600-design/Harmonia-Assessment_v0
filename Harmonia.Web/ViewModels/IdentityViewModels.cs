using System.ComponentModel.DataAnnotations;
namespace Harmonia.Web.ViewModels;
public sealed class LoginInput
{
    [Required, StringLength(256)] public string UserName { get; set; } = "";
    [Required, DataType(DataType.Password)] public string Password { get; set; } = "";
    public string? ReturnUrl { get; set; }
}
public sealed class PasswordInput
{
    [Required, DataType(DataType.Password)] public string CurrentPassword { get; set; } = "";
    [Required, StringLength(128, MinimumLength=12), DataType(DataType.Password)] public string NewPassword { get; set; } = "";
    [Compare(nameof(NewPassword)), DataType(DataType.Password)] public string ConfirmPassword { get; set; } = "";
}
public sealed class TeacherInput
{
    [Required, StringLength(50)] public string TeacherCode { get; set; } = "";
    [Required, StringLength(150)] public string FullName { get; set; } = "";
    [Required, EmailAddress, StringLength(150)] public string Email { get; set; } = "";
    [Required, StringLength(20)] public string PhoneNumber { get; set; } = "";
}
public sealed class TeacherAccountInput
{
    [Required, StringLength(100)] public string UserName { get; set; } = "";
    [Required, StringLength(128, MinimumLength=12), DataType(DataType.Password)] public string TemporaryPassword { get; set; } = "";
}
public sealed class ResetTeacherPasswordInput
{
    [Required, StringLength(128, MinimumLength=12), DataType(DataType.Password)] public string TemporaryPassword { get; set; } = "";
}
public sealed class TeacherAssignmentInput
{
    [Range(1,long.MaxValue, ErrorMessage="Chọn lớp đang hoạt động.")] public long ClassId { get; set; }
    [Required(ErrorMessage="Chọn ngày bắt đầu phân công.")] public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
}
public sealed record TeacherListRow(long Id, string Code, string Name, string Status)
{
    public IReadOnlyList<TeacherAssignmentRow> Assignments { get; init; } = [];
    public bool HasAccount { get; init; }
    public bool AccountActive { get; init; }
}
public sealed record TeacherAssignmentRow(long ClassId,string ClassName,DateOnly StartDate,DateOnly? EndDate,bool Current)
{
    public string AcademicYear { get; init; } = "";
    public bool CanOpenClass { get; init; }
}
public sealed record TeacherDetailsPage(long Id,string Code,string Name,string Email,string Phone,string Status,
    bool HasAccount,bool AccountActive,string? UserName,IReadOnlyList<TeacherAssignmentRow> Assignments,IReadOnlyList<AuthoringClass> Classes)
{
    public TeacherAssignmentInput Assignment { get; init; } = new();
    public AcademicClassHeader? AssignmentClassContext { get; init; }
    public bool AccountIsManager { get; init; }
    public bool IsOwnAccount { get; init; }
    public bool CanLinkOwnManager { get; init; }
}
public sealed record ManagerTeacherLinkResult(bool Succeeded, string Message);
public sealed record TeacherAssignmentResult(bool Succeeded, string Message, string? Field = null);

public static class AssignmentDates
{
    public static string Label(DateOnly start, DateOnly? end, DateOnly asOf) =>
        start > asOf ? "Sắp bắt đầu" : end.HasValue && end.Value <= asOf ? "Đã kết thúc" : "Đang phụ trách";

    public static TeacherAssignmentResult? Validate(TeacherAssignmentInput input) =>
        input.ClassId <= 0 ? new(false, "Chọn lớp đang hoạt động.", "ClassId")
        : !input.StartDate.HasValue ? new(false, "Chọn ngày bắt đầu phân công.", "StartDate")
        : input.EndDate.HasValue && input.EndDate <= input.StartDate ? new(false, "Ngày kết thúc phải sau ngày bắt đầu; ngày kết thúc không thuộc khoảng phân công.", "EndDate")
        : null;
}
