using System;
using System.Collections.Generic;

namespace Harmonia.Web.Models.Entities;

public partial class Assessment
{
    public long AssessmentId { get; set; }

    public long TestId { get; set; }

    public long ClassId { get; set; }

    public string CreatedByUserId { get; set; } = null!;

    public string? UpdatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string AssessmentName { get; set; } = null!;

    public DateOnly AssessmentDate { get; set; }

    public string Status { get; set; } = null!;

    public long TeacherId { get; set; }

    public virtual ICollection<Attempt> Attempts { get; set; } = new List<Attempt>();

    public virtual Class Class { get; set; } = null!;

    public virtual ICollection<ImportBatch> ImportBatches { get; set; } = new List<ImportBatch>();

    public virtual ICollection<InterventionPackageFollowup> InterventionPackageFollowups { get; set; } = new List<InterventionPackageFollowup>();

    public virtual ICollection<InterventionPackage> InterventionPackages { get; set; } = new List<InterventionPackage>();

    public virtual Teacher Teacher { get; set; } = null!;

    public virtual Test Test { get; set; } = null!;
}
