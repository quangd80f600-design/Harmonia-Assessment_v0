using System.ComponentModel.DataAnnotations;

namespace Harmonia.Web.ViewModels;

public sealed class CreateClassInput
{
    [Required(ErrorMessage = "Nhập mã lớp."), StringLength(50), Display(Name = "Mã lớp")]
    public string ClassCode { get; set; } = "";
    [Required(ErrorMessage = "Nhập tên lớp."), StringLength(150), Display(Name = "Tên lớp")]
    public string ClassName { get; set; } = "";
    [Range(6, 12, ErrorMessage = "Khối lớp phải từ 6 đến 12."), Display(Name = "Khối lớp")]
    public short Grade { get; set; } = 9;
    [Required(ErrorMessage = "Nhập năm học."), StringLength(9), Display(Name = "Năm học")]
    public string AcademicYear { get; set; } = "";
}

public sealed record AcademicClassHeader(long ClassId, string ClassCode, string ClassName,
    short Grade, string AcademicYear, string Status);
public sealed record AcademicClassListRow(AcademicClassHeader Class, int StudentCount)
{
    public IReadOnlyList<ClassTeacherAssignmentRow> Teachers { get; init; } = [];
}
public sealed record AcademicClassListViewModel(DateOnly AsOf, bool CanWrite, IReadOnlyList<AcademicClassListRow> Rows)
{
    public bool MineOnly { get; init; }
    public bool CanViewMine { get; init; }
}
public sealed record ClassStudentRow(long StudentId, string StudentCode, string FullName,
    DateOnly DateOfBirth, string Gender, string Status, DateOnly StartDate, DateOnly? EndDate);
public sealed record ClassDetailsViewModel(AcademicClassHeader Class, DateOnly AsOf,
    bool CanImport, IReadOnlyList<ClassStudentRow> Students)
{
    public IReadOnlyList<ClassTeacherAssignmentRow> Teachers { get; init; } = [];
    public IReadOnlyList<ClassAssessmentRow> Assessments { get; init; } = [];
    public bool CanCreateAssessment { get; init; }
    public bool CanAssignTeacher { get; init; }
}
public sealed record ClassTeacherAssignmentRow(long TeacherId, string TeacherCode, string TeacherName,
    DateOnly StartDate, DateOnly? EndDate, bool CanOpenTeacher);
public sealed record ClassAssessmentRow(long Id, string Name, string TestName, DateOnly Date, string Status);
public sealed record ClassCreation(long? ClassId, IReadOnlyList<ImportError> Errors);
public sealed record ClassDeletion(bool Deleted, bool NotFound, string Message);
public sealed record RosterStudentPreview(string StudentCode, string FullName, DateOnly DateOfBirth,
    string Gender, DateOnly StartDate, DateOnly? EndDate, long? ExistingStudentId, string Status);
public sealed record RosterPreviewViewModel(AcademicClassHeader Class, string FileName, string Token,
    DateTimeOffset ExpiresAt, IReadOnlyList<RosterStudentPreview> Students)
{
    public int NewCount => Students.Count(s => s.ExistingStudentId is null);
    public int ExistingCount => Students.Count - NewCount;
}
public sealed record RosterImportPage(AcademicClassHeader Class, bool CanImport, string DemoActor,
    IReadOnlyList<ImportError> Errors);
public sealed record RosterPreparation(RosterImportPage Page, RosterPreviewViewModel? Preview);
public sealed record RosterImportOutcome(AcademicClassHeader Class, bool Succeeded, int NewStudents,
    int ExistingStudents, int Memberships, IReadOnlyList<ImportError> Errors, bool CommitUncertain = false);
