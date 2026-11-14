using System;
using System.Collections.Generic;

namespace Harmonia.Web.Models.Entities;

public partial class TeacherClassAssignment
{
    public long TeacherClassAssignmentId { get; set; }

    public long ClassId { get; set; }

    public long TeacherId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string CreatedByUserId { get; set; } = null!;

    public string? UpdatedByUserId { get; set; }

    public virtual Class Class { get; set; } = null!;

    public virtual Teacher Teacher { get; set; } = null!;
}
