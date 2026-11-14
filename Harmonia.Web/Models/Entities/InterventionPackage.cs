using System;
using System.Collections.Generic;

namespace Harmonia.Web.Models.Entities;

public partial class InterventionPackage
{
    public long InterventionPackageId { get; set; }

    public long AssessmentId { get; set; }

    public string PackageName { get; set; } = null!;

    public string? Instructions { get; set; }

    public string Status { get; set; } = null!;

    public virtual Assessment Assessment { get; set; } = null!;

    public virtual ICollection<InterventionPackageFollowup> InterventionPackageFollowups { get; set; } = new List<InterventionPackageFollowup>();

    public virtual ICollection<InterventionPackageResource> InterventionPackageResources { get; set; } = new List<InterventionPackageResource>();

    public virtual ICollection<InterventionPackageStudent> InterventionPackageStudents { get; set; } = new List<InterventionPackageStudent>();
}
