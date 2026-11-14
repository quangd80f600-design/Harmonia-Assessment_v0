using System;
using System.Collections.Generic;

namespace Harmonia.Web.Models.Entities;

public partial class InterventionResource
{
    public long InterventionResourceId { get; set; }

    public long SubskillId { get; set; }

    public string ResourceName { get; set; } = null!;

    public string ResourceUrl { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string Status { get; set; } = null!;

    public virtual ICollection<InterventionPackageResource> InterventionPackageResources { get; set; } = new List<InterventionPackageResource>();

    public virtual Subskill Subskill { get; set; } = null!;
}
