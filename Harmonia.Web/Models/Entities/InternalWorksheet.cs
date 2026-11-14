namespace Harmonia.Web.Models.Entities;

public sealed class InterventionTheory
{
    public long InterventionTheoryId { get; set; }
    public long SubskillId { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string Status { get; set; } = "Active";
}
public sealed class InterventionPackageTheory
{
    public long InterventionPackageTheoryId { get; set; }
    public long InterventionPackageId { get; set; }
    public long InterventionTheoryId { get; set; }
    public short SortOrder { get; set; }
    public string SnStitle { get; set; } = "";
    public string SnScontent { get; set; } = "";
    public long SnSsubskillId { get; set; }
}
public sealed class InterventionPackageQuestion
{
    public long InterventionPackageQuestionId { get; set; }
    public long InterventionPackageId { get; set; }
    public long QuestionId { get; set; }
    public short QuestionNumber { get; set; }
    public string SnSquestionText { get; set; } = "";
    public string SnSoptionA { get; set; } = "";
    public string SnSoptionB { get; set; } = "";
    public string SnSoptionC { get; set; } = "";
    public string SnSoptionD { get; set; } = "";
    public string SnSanswerKey { get; set; } = "";
    public string SnSexplanation { get; set; } = "";
    public long SnSsubskillId { get; set; }
}
