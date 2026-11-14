using System;
using System.Collections.Generic;

namespace Harmonia.Web.Models.Entities;

public partial class InterventionPackageStudent
{
    public long InterventionPackageStudentId { get; set; }

    public long InterventionPackageId { get; set; }

    public long StudentId { get; set; }

    public DateOnly? DeliveredDate { get; set; }

    public string? TeacherNote { get; set; }

    public virtual InterventionPackage InterventionPackage { get; set; } = null!;

    public virtual Student Student { get; set; } = null!;
}
