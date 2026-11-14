using System.ComponentModel.DataAnnotations;

namespace Harmonia.Web.ViewModels;

public sealed class TaxonomyFilter
{
    [Range(6, 12)] public short? Grade { get; set; }
    [Range(1, long.MaxValue)] public long? UnitId { get; set; }
    [Range(1, long.MaxValue)] public long? AreaId { get; set; }
}
public sealed record TaxonomySkill(long Id, string Code, string Name, string Description, bool Active);
public sealed record TaxonomyArea(long Id, string Code, string Name, bool Active, IReadOnlyList<TaxonomySkill> Skills);
public sealed record TaxonomyUnit(long Id, short Grade, short Number, string Name, IReadOnlyList<TaxonomyArea> Areas);
public sealed record TaxonomyPage(TaxonomyFilter Filter, IReadOnlyList<TaxonomyUnit> Units);
public sealed record SubskillChoice(long Id, short Grade, string Caption);

public sealed class QuestionSearch
{
    [RegularExpression("^(ForTest|ForIntervention)$", ErrorMessage = "Mục đích sử dụng không hợp lệ.")]
    public string? UsagePurpose { get; set; }
    [StringLength(100)] public string? Term { get; set; }
    [Range(6, 12)] public short? Grade { get; set; }
    [Range(1, long.MaxValue)] public long? SubskillId { get; set; }
    [Range(1, 1000000)] public int Page { get; set; } = 1;
    [Range(1, long.MaxValue)] public long? UnitId { get; set; }
    [Range(1, long.MaxValue)] public long? AreaId { get; set; }
    public bool ClearSubskill { get; set; }
}
public sealed record QuestionListRow(long Id, string Code, string Text, long SubskillId, string SubskillName,
    short Grade, short Difficulty, string Status, int TestCount)
{
    public string UsagePurpose { get; init; } = "ForTest";
}
public sealed record QuestionBankPage(QuestionSearch Filter, IReadOnlyList<QuestionListRow> Rows, int Total,
    string? Notice = null, string? SelectedSubskill = null) { public string KnowledgePath { get; init; } = "Tất cả câu hỏi"; };
public sealed class QuestionInput
{
    [Required, RegularExpression("^(ForTest|ForIntervention)$", ErrorMessage = "Chọn mục đích Đánh giá hoặc Luyện tập can thiệp.")]
    public string UsagePurpose { get; set; } = "ForTest";
    [Required(ErrorMessage = "Nhập mã câu hỏi."), StringLength(50)] public string QuestionCode { get; set; } = "";
    [Required(ErrorMessage = "Nhập nội dung câu hỏi."), StringLength(10000)] public string QuestionText { get; set; } = "";
    [Required, StringLength(5000)] public string OptionA { get; set; } = "";
    [Required, StringLength(5000)] public string OptionB { get; set; } = "";
    [Required, StringLength(5000)] public string OptionC { get; set; } = "";
    [Required, StringLength(5000)] public string OptionD { get; set; } = "";
    [Required, RegularExpression("^[ABCD]$", ErrorMessage = "Đáp án đúng phải là A, B, C hoặc D.")] public string AnswerKey { get; set; } = "A";
    [Range(1, 3)] public short DifficultyLevel { get; set; } = 1;
    [Range(1, long.MaxValue, ErrorMessage = "Chọn Subskill hợp lệ.")] public long SubskillId { get; set; }
    [StringLength(10000)] public string? Explanation { get; set; } = "";
}
public sealed record QuestionCreatePage(QuestionInput Input, IReadOnlyList<SubskillChoice> Subskills);
public sealed record QuestionDetailsPage(long Id, QuestionInput Question, string SubskillCaption, short Grade, string Status, int TestCount) { public long? UnitId { get; init; } public long? AreaId { get; init; } }
public sealed record QuestionEditPage(long Id, QuestionInput Input, IReadOnlyList<SubskillChoice> Subskills, string Version);

public sealed class TestPick
{
    [Range(1, long.MaxValue)] public long QuestionId { get; set; }
    [Range(1, 200, ErrorMessage = "Số câu phải từ 1 đến 200; để trống nếu không chọn.")] public short? Number { get; set; }
}
public sealed class TestInput
{
    [Required(ErrorMessage = "Nhập tên đề."), StringLength(255)] public string TestName { get; set; } = "";
    [StringLength(10000)] public string? Description { get; set; }
    [Range(6, 12)] public short Grade { get; set; } = 9;
    [Required, MaxLength(500)] public List<TestPick> Picks { get; set; } = [];
    [StringLength(100)] public string? Search { get; set; }
    [StringLength(64)] public string? Version { get; set; }
}
public sealed record TestComposePage(TestInput Input, string? Search, IReadOnlyList<QuestionListRow> Questions, bool HasMore,
    long? EditId = null);
public sealed class CopyTestInput
{
    [Required, StringLength(255)] public string TestName { get; set; } = "";
    [StringLength(10000)] public string? Description { get; set; }
}
public sealed record TestListRow(long Id, string Name, string Status, int QuestionCount, int AssessmentCount);
public sealed record TestSnapshotRow(long QuestionId, short Number, string Text, string A, string B, string C, string D,
    string Key, long SubskillId, string SubskillCode, string SubskillName, short Grade);
public sealed record TestDetailsPage(long Id, string Name, string? Description, string Status, int AssessmentCount,
    IReadOnlyList<TestSnapshotRow> Questions);
public sealed record TestCopyPage(long SourceId, string SourceName, CopyTestInput Input);

public sealed class AssessmentCreateInput
{
    [Required(ErrorMessage = "Nhập tên đợt đánh giá."), StringLength(255)] public string AssessmentName { get; set; } = "";
    [Range(1, long.MaxValue)] public long ClassId { get; set; }
    [Range(1, long.MaxValue)] public long TestId { get; set; }
    [Range(1, long.MaxValue)] public long TeacherId { get; set; }
    [Required] public DateOnly? AssessmentDate { get; set; }
    public DateOnly? PlannedFollowupDate { get; set; }
}
public sealed record AuthoringClass(long Id, string Name, short Grade, string AcademicYear);
public sealed record AuthoringTeacher(long Id, string Code, string Name);
public sealed record AuthoringAssignment(long ClassId, long TeacherId, DateOnly StartDate, DateOnly? EndDate);
public sealed record AssessmentCreatePage(AssessmentCreateInput Input, IReadOnlyList<AuthoringClass> Classes,
    IReadOnlyList<TestListRow> Tests, IReadOnlyList<AuthoringTeacher> Teachers, IReadOnlyList<AuthoringAssignment> Assignments);
public sealed record AuthoringWriteResult(bool Succeeded, string Message, long? Id = null);
