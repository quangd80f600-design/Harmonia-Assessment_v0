using System.ComponentModel.DataAnnotations.Schema;

namespace Harmonia.Web.Models.Entities;

public partial class Assessment
{
    [Column("PlannedFollowupDate", TypeName = "date")]
    public DateOnly? PlannedFollowupDate { get; set; }
}
