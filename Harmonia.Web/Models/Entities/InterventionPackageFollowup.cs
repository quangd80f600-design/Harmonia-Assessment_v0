using System;
using System.Collections.Generic;

namespace Harmonia.Web.Models.Entities;

public partial class InterventionPackageFollowup
{
    public long InterventionPackageFollowupId { get; set; }

    public long FollowupAssessmentId { get; set; }

    public long InterventionPackageId { get; set; }

    public string? FollowupNote { get; set; }

    public virtual Assessment FollowupAssessment { get; set; } = null!;

    public virtual InterventionPackage InterventionPackage { get; set; } = null!;
}
