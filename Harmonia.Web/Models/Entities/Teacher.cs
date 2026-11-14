using System;
using System.Collections.Generic;

namespace Harmonia.Web.Models.Entities;

public partial class Teacher
{
    public long TeacherId { get; set; }

    public string TeacherCode { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PhoneNumber { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string CreatedByUserId { get; set; } = null!;

    public string? UpdatedByUserId { get; set; }

    public virtual ICollection<Assessment> Assessments { get; set; } = new List<Assessment>();

    public virtual ICollection<TeacherClassAssignment> TeacherClassAssignments { get; set; } = new List<TeacherClassAssignment>();
}
