using System.ComponentModel.DataAnnotations;

namespace Harmonia.Web.ViewModels;

public sealed class TheoryInput
{
    [Range(1,long.MaxValue)] public long SubskillId { get; set; }
    [Required, StringLength(200)] public string Title { get; set; } = "";
    [Required, StringLength(20000)] public string Content { get; set; } = "";
    public string Status { get; set; } = "Active";
    public string Version { get; set; } = "";
}
public sealed record TheoryChoice(long Id, long SubskillId, string Title, string Content, string Status);
public sealed record TheorySkill(long Id, string Code, string Name)
{
    public short Grade { get; init; }
    public long UnitId { get; init; }
    public string UnitName { get; init; } = "";
    public short UnitNumber { get; init; }
    public long AreaId { get; init; }
    public string AreaName { get; init; } = "";
}
public sealed record TheoryPage(TheoryInput Input, long? Id, bool CanWrite, IReadOnlyList<TheoryChoice> Rows, IReadOnlyList<TheorySkill> Skills)
{
    public TheoryBrowseFilter Filter { get; init; } = new();
    public string? FilterNotice { get; init; }
}
public sealed class TheoryBrowseFilter
{
    [Range(6,12)] public short? Grade { get; set; }
    [Range(1,long.MaxValue)] public long? UnitId { get; set; }
    [Range(1,long.MaxValue)] public long? AreaId { get; set; }
    [Range(1,long.MaxValue)] public long? SubskillId { get; set; }
    [StringLength(100)] public string? Search { get; set; }
}
public sealed class WorksheetPick
{
    public long Id { get; set; }
    public bool Selected { get; set; }
    public int Order { get; set; }
}
public class WorksheetInput
{
    [Required(ErrorMessage="Nhập tên phiếu ôn tập."), StringLength(200,ErrorMessage="Tên phiếu tối đa 200 ký tự.")] public string PackageName { get; set; } = "";
    [StringLength(10000)] public string? Instructions { get; set; }
    [Required(ErrorMessage="Phiên soạn không còn hợp lệ. Hãy mở lại trang."), StringLength(64, MinimumLength=64)] public string Version { get; set; } = "";
    [MaxLength(200,ErrorMessage="Mỗi phiếu tối đa 200 mục lý thuyết.")] public List<WorksheetPick> Theories { get; set; } = [];
    [MaxLength(200,ErrorMessage="Mỗi phiếu tối đa 200 câu luyện.")] public List<WorksheetPick> Questions { get; set; } = [];
}
public sealed record WorksheetQuestion(long Id, long SubskillId, string Code, string Text, string A, string B, string C, string D, string Key, string Explanation);
public sealed record WorksheetSkill(long Id, string Code, string Name, int QuestionCount, int ResponseCount, int IncorrectCount);
public sealed record WorksheetComposer(long AssessmentId, long StudentId, string StudentName, string AssessmentName,
    long? PackageId, WorksheetInput Input, IReadOnlyList<WorksheetSkill> Skills,
    IReadOnlyList<TheoryChoice> Theories, IReadOnlyList<WorksheetQuestion> Questions)
{
    public bool HasAttempt { get; init; }
    public int QuestionCount { get; init; }
    public int ResponseCount { get; init; }
}
// Student-facing DTOs intentionally have no answer, explanation or internal assignment note.
public sealed record WorksheetPublicQuestion(int Number, long SubskillId, string Text, string A, string B, string C, string D);
public sealed record WorksheetTheory(int Order, long SubskillId, string Title, string Content);
public sealed record WorksheetAnswer(int Number, string Key, string Explanation);
public sealed record WorksheetPrint(long PackageId, string Name, string? Instructions, string ClassName, string AssessmentName,
    string? StudentName, string Status, IReadOnlyList<TheorySkill> Skills,
    IReadOnlyList<WorksheetTheory> Theories, IReadOnlyList<WorksheetPublicQuestion> Questions);
public sealed record WorksheetAnswerPage(long PackageId, string Name, IReadOnlyList<WorksheetAnswer> Answers);
public sealed record WorksheetWrite(bool Succeeded, string Message, long? Id = null);

public sealed class WorksheetGroupInput : WorksheetInput
{
    public long[] SubskillIds { get; set; } = [];
    public string MatchMode { get; set; } = "Any";
    [Required, MinLength(1, ErrorMessage="Chọn ít nhất một học sinh."), MaxLength(200, ErrorMessage="Mỗi lần giao tối đa 200 học sinh.")]
    public long[] StudentIds { get; set; } = [];
}
public sealed record WorksheetGroupCell(long SubskillId,int CorrectCount,int ResponseCount,int QuestionCount);
public sealed record WorksheetGroupStudent(long StudentId,string StudentCode,string FullName,int CorrectCount,int ResponseCount,int QuestionCount)
{ public IReadOnlyList<WorksheetGroupCell> Cells { get; init; } = []; }
public sealed record WorksheetGroupPage(WorksheetComposer Content, IReadOnlyList<WorksheetGroupStudent> Students,
    int RosterCount,int MissingAttemptCount,int IncompleteResponseCount,decimal? ClassErrorPercentage)
{
    public string ClassName { get; init; } = "";
    public IReadOnlyList<TheorySkill> AvailableSkills { get; init; } = [];
}
public sealed record WorksheetGroupDraft(long PackageId,long AssessmentId,long SubskillId,IReadOnlyList<long> StudentIds,
    string BasisVersion,string DraftVersion)
{
    public IReadOnlyList<long> SubskillIds { get; init; } = [];
    public string MatchMode { get; init; } = "Any";
}
public sealed record WorksheetGroupSave(WorksheetWrite Result,WorksheetGroupDraft? Draft=null);
public sealed record WorksheetGroupReview(long PackageId,string PackageName,long AssessmentId,long SubskillId,
    IReadOnlyList<WorksheetGroupStudent> Students,int QuestionCount,int TheoryCount,bool CanConfirm,string? Message)
{ public IReadOnlyList<TheorySkill> Skills {get;init;}=[]; public string MatchMode {get;init;}="Any"; }

public sealed record WorksheetExactGroup(IReadOnlyList<long> SubskillIds,IReadOnlyList<TheorySkill> Skills,IReadOnlyList<WorksheetGroupStudent> Students);
public sealed record WorksheetExactGroupsPage(long AssessmentId,string AssessmentName,string ClassName,int RosterCount,
    int CorrectOnlyCount,int MissingCount,int IncompleteCount,IReadOnlyList<WorksheetExactGroup> Groups);
