using System;
using System.Collections.Generic;

namespace Harmonia.Web.Models.Entities;

public partial class Test
{
    public long TestId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string CreatedByUserId { get; set; } = null!;

    public string? UpdatedByUserId { get; set; }

    public string TestName { get; set; } = null!;

    public string? Description { get; set; }

    public string Status { get; set; } = null!;

    public virtual ICollection<Assessment> Assessments { get; set; } = new List<Assessment>();

    public virtual ICollection<QuestionInTest> QuestionInTests { get; set; } = new List<QuestionInTest>();
}
