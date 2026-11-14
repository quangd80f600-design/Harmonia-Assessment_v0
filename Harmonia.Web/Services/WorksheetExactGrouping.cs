using Harmonia.Web.ViewModels;
namespace Harmonia.Web.Services;

// A profile is the SET of snapshot Subskill IDs containing an explicit incorrect Response.
// Only a full item-level result can be assigned an exact profile.
public static class WorksheetExactGrouping
{
    public static long[] WrongSkills(WorksheetGroupStudent student) => student.Cells
        .Where(c=>c.ResponseCount>c.CorrectCount).Select(c=>c.SubskillId).Distinct().Order().ToArray();
    public static bool Complete(WorksheetGroupStudent student) => student.QuestionCount>0
        && student.ResponseCount==student.QuestionCount && student.Cells.Sum(c=>c.QuestionCount)==student.QuestionCount
        && student.Cells.All(c=>c.ResponseCount==c.QuestionCount && c.CorrectCount>=0 && c.CorrectCount<=c.ResponseCount);
    public static bool Matches(WorksheetGroupStudent student,IEnumerable<long> selected) => Complete(student)
        && WrongSkills(student).Length>0 && WrongSkills(student).SequenceEqual(selected.Distinct().Order());
    public static IReadOnlyList<WorksheetExactGroup> Partition(IEnumerable<WorksheetGroupStudent> students,IReadOnlyList<TheorySkill> skills)
        => students.Where(s=>Complete(s)&&WrongSkills(s).Length>0)
            .GroupBy(s=>string.Join(",",WrongSkills(s)))
            .Select(g=>new WorksheetExactGroup(WrongSkills(g.First()),
                skills.Where(s=>WrongSkills(g.First()).Contains(s.Id)).OrderBy(s=>s.Id).ToArray(),g.OrderBy(s=>s.StudentCode,StringComparer.Ordinal).ToArray()))
            .OrderByDescending(g=>g.Students.Count).ThenBy(g=>string.Join(",",g.SubskillIds),StringComparer.Ordinal).ToArray();
}
