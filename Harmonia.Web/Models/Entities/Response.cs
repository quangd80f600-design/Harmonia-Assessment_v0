using System;
using System.Collections.Generic;

namespace Harmonia.Web.Models.Entities;

public partial class Response
{
    public long ResponseId { get; set; }

    public long AttemptId { get; set; }

    public long QuestionInTestId { get; set; }

    public bool IsCorrect { get; set; }

    public virtual Attempt Attempt { get; set; } = null!;

    public virtual QuestionInTest QuestionInTest { get; set; } = null!;
}
