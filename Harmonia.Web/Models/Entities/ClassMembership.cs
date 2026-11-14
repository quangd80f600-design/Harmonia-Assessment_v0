using System;
using System.Collections.Generic;

namespace Harmonia.Web.Models.Entities;

public partial class ClassMembership
{
    public long ClassMembershipId { get; set; }

    public long StudentId { get; set; }

    public long ClassId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string CreatedByUserId { get; set; } = null!;

    public string? UpdatedByUserId { get; set; }

    public virtual Class Class { get; set; } = null!;

    public virtual Student Student { get; set; } = null!;
}
