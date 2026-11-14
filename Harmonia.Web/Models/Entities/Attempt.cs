using System;
using System.Collections.Generic;

namespace Harmonia.Web.Models.Entities;

public partial class Attempt
{
    public long AttemptId { get; set; }

    public long AssessmentId { get; set; }

    public long StudentId { get; set; }

    public long ImportBatchId { get; set; }

    public decimal Score { get; set; }

    public virtual Assessment Assessment { get; set; } = null!;

    public virtual ImportBatch ImportBatch { get; set; } = null!;

    public virtual ICollection<Response> Responses { get; set; } = new List<Response>();

    public virtual Student Student { get; set; } = null!;
}
