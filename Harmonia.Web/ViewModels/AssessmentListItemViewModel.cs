namespace Harmonia.Web.ViewModels;

public sealed record AssessmentListItemViewModel(long AssessmentId, string AssessmentName,
    DateOnly AssessmentDate, string Status, ImportPageViewModel? Import)
{
    public ResultCompletion? ResultCompletion => Import?.ResultCompletion;
    public bool CanImportSupplement => ResultCompletion?.HasMissingResults == true && Import?.CanImport == true;
}

public sealed record AssessmentCreationGuidanceViewModel(
    bool IsManager, bool HasClasses, bool HasAssignments, bool HasPublishedTests)
{
    public bool NeedsPreparation => !HasClasses || !HasAssignments || !HasPublishedTests;

    // Use the existing, permission-scoped form choices. This is guidance, not a new creation gate.
    public static AssessmentCreationGuidanceViewModel From(AssessmentCreatePage form, bool isManager) =>
        new(isManager, form.Classes.Count > 0,
            form.Assignments.Any(a => form.Classes.Any(c => c.Id == a.ClassId)
                && form.Teachers.Any(t => t.Id == a.TeacherId)),
            form.Tests.Count > 0);
}
