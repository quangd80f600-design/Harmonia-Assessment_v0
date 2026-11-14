using System;
using System.Collections.Generic;

namespace Harmonia.Web.Models.Entities;

public partial class QuestionInTest
{
    public long QuestionInTestId { get; set; }

    public long QuestionId { get; set; }

    public long TestId { get; set; }

    public string SnSquestionText { get; set; } = null!;

    public string SnSoptionA { get; set; } = null!;

    public string SnSoptionB { get; set; } = null!;

    public string SnSoptionC { get; set; } = null!;

    public string SnSoptionD { get; set; } = null!;

    public string SnSanswerKey { get; set; } = null!;

    public long SnSsubskillId { get; set; }

    public short QuestionNumber { get; set; }

    public virtual Question Question { get; set; } = null!;

    public virtual ICollection<Response> Responses { get; set; } = new List<Response>();

    public virtual Subskill SnSsubskill { get; set; } = null!;

    public virtual Test Test { get; set; } = null!;
}
