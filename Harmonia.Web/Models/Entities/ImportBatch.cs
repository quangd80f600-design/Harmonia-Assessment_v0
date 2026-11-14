using System;
using System.Collections.Generic;

namespace Harmonia.Web.Models.Entities;

public partial class ImportBatch
{
    public long ImportBatchId { get; set; }

    public long AssessmentId { get; set; }

    public string OriginalFileName { get; set; } = null!;

    public string ImportedByUserId { get; set; } = null!;

    public string Status { get; set; } = null!;

    public int TotalRows { get; set; }

    public string? ErrorSummary { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public virtual Assessment Assessment { get; set; } = null!;

    public virtual ICollection<Attempt> Attempts { get; set; } = new List<Attempt>();
}
