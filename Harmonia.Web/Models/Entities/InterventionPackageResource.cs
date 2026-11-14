using System;
using System.Collections.Generic;

namespace Harmonia.Web.Models.Entities;

public partial class InterventionPackageResource
{
    public long InterventionPackageResourceId { get; set; }

    public long InterventionPackageId { get; set; }

    public long InterventionResourceId { get; set; }

    public short SortOrder { get; set; }

    public virtual InterventionPackage InterventionPackage { get; set; } = null!;

    public virtual InterventionResource InterventionResource { get; set; } = null!;
}
