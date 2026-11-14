using System;
using System.Collections.Generic;

namespace Harmonia.Web.Models.Entities;

public partial class Question
{
    public long QuestionId { get; set; }

    public long SubskillId { get; set; }

    public string QuestionCode { get; set; } = null!;

    public string QuestionText { get; set; } = null!;

    public string OptionA { get; set; } = null!;

    public string OptionB { get; set; } = null!;

    public string OptionC { get; set; } = null!;

    public string OptionD { get; set; } = null!;

    public string AnswerKey { get; set; } = null!;

    public short DifficultyLevel { get; set; }

    public string Explanation { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string UsagePurpose { get; set; } = "ForTest";

    public virtual ICollection<QuestionInTest> QuestionInTests { get; set; } = new List<QuestionInTest>();

    public virtual Subskill Subskill { get; set; } = null!;
}
