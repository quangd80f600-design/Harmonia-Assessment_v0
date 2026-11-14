using System;
using System.Collections.Generic;

namespace Harmonia.Web.Models.Entities;

public partial class CurriculumUnit
{
    public long CurriculumUnitId { get; set; }

    public short Grade { get; set; }

    public short UnitNumber { get; set; }

    public string UnitName { get; set; } = null!;

    public virtual ICollection<KnowledgeArea> KnowledgeAreas { get; set; } = new List<KnowledgeArea>();
}
