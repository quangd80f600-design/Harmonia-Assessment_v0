using System;
using System.Collections.Generic;

namespace Harmonia.Web.Models.Entities;

public partial class Subskill
{
    public long SubskillId { get; set; }

    public long KnowledgeAreaId { get; set; }

    public string SubskillCode { get; set; } = null!;

    public string SubskillName { get; set; } = null!;

    public string Description { get; set; } = null!;

    public short SortOrder { get; set; }

    public string Status { get; set; } = null!;

    public virtual ICollection<InterventionResource> InterventionResources { get; set; } = new List<InterventionResource>();

    public virtual KnowledgeArea KnowledgeArea { get; set; } = null!;

    public virtual ICollection<QuestionInTest> QuestionInTests { get; set; } = new List<QuestionInTest>();

    public virtual ICollection<Question> Questions { get; set; } = new List<Question>();
}
