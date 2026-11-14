using System;
using System.Collections.Generic;

namespace Harmonia.Web.Models.Entities;

public partial class KnowledgeArea
{
    public long KnowledgeAreaId { get; set; }

    public string KnowledgeAreaCode { get; set; } = null!;

    public string KnowledgeAreaName { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string Status { get; set; } = null!;

    public long CurriculumUnitId { get; set; }

    public short SortOrder { get; set; }

    public virtual CurriculumUnit CurriculumUnit { get; set; } = null!;

    public virtual ICollection<Subskill> Subskills { get; set; } = new List<Subskill>();
}
