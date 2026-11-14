using System;
using System.Collections.Generic;

namespace Harmonia.Web.Models.Entities;

public partial class Class
{
    public long ClassId { get; set; }

    public string ClassCode { get; set; } = null!;

    public string ClassName { get; set; } = null!;

    public short Grade { get; set; }

    public string AcademicYear { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string CreatedByUserId { get; set; } = null!;

    public string? UpdatedByUserId { get; set; }

    public virtual ICollection<Assessment> Assessments { get; set; } = new List<Assessment>();

    public virtual ICollection<ClassMembership> ClassMemberships { get; set; } = new List<ClassMembership>();

    public virtual ICollection<TeacherClassAssignment> TeacherClassAssignments { get; set; } = new List<TeacherClassAssignment>();
}
