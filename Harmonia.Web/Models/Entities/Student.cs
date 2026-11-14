using System;
using System.Collections.Generic;

namespace Harmonia.Web.Models.Entities;

public partial class Student
{
    public long StudentId { get; set; }

    public string StudentCode { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public DateOnly DateOfBirth { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string Gender { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string? PrimaryGuardianName { get; set; }

    public string? PrimaryGuardianPhonenumber { get; set; }

    public string CreatedByUserId { get; set; } = null!;

    public string? UpdatedByUserId { get; set; }

    public virtual ICollection<Attempt> Attempts { get; set; } = new List<Attempt>();

    public virtual ICollection<ClassMembership> ClassMemberships { get; set; } = new List<ClassMembership>();

    public virtual ICollection<InterventionPackageStudent> InterventionPackageStudents { get; set; } = new List<InterventionPackageStudent>();
}
