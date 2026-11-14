using Harmonia.Web.Data;
using Harmonia.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
namespace Harmonia.Web.Services.Security;
// Fail closed for new controllers. This is the MVC resource boundary; service scopes also filter lists.
public sealed class BusinessAuthorizationFilter(CurrentActor actor, BusinessAccess access, HarmoniaIdentityDbContext identity) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext c, ActionExecutionDelegate next)
    {
        if (c.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any()) { await next(); return; }
        if (actor.Id is null) { c.Result = new ChallengeResult(); return; }
        foreach (var name in new[] { "id", "assessmentId", "studentId", "assignmentId" })
        {
            if (c.ActionArguments.TryGetValue(name, out var value)
                && (value is long number && number <= 0 || c.ModelState.TryGetValue(name, out var state) && state.Errors.Count != 0))
            { c.Result = new BadRequestResult(); return; }
        }
        var controller = c.RouteData.Values["controller"]?.ToString(); var action = c.RouteData.Values["action"]?.ToString();
        var user = await identity.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == actor.Id, c.HttpContext.RequestAborted);
        if (user is null || !user.IsActive || !actor.IsManager && actor.TeacherId is null) { c.Result = new ForbidResult(); return; }
        if (user.MustChangePassword && !(controller == "Account" && action is "ChangePassword" or "Logout" or "AccessDenied"))
        {
            c.Result = HttpMethods.IsGet(c.HttpContext.Request.Method) && !c.HttpContext.Request.Path.StartsWithSegments("/Authoring/Workspace")
                ? new RedirectToActionResult("ChangePassword", "Account", null) : new StatusCodeResult(403); return;
        }
        if (controller == "Account") { await next(); return; }
        var ct = c.HttpContext.RequestAborted; var write = !HttpMethods.IsGet(c.HttpContext.Request.Method);
        long Id(string name) => c.ActionArguments.TryGetValue(name, out var value) && value is long id ? id : 0;
        bool allowed;
        switch (controller)
        {
            case "Worksheets":
                allowed = action == "Theories" ? (!write || actor.IsManager || !c.ActionArguments.TryGetValue("theoryId", out var theoryId) || theoryId is null)
                    : action is "Compose" or "Group" or "ExactGroups" ? await access.AssessmentAsync(Id("assessmentId"), true, ct)
                    : action == "Review" ? await access.PackageAsync(Id("id"), true, ct)
                    : action == "Edit" ? await access.PackageAsync(Id("id"), true, ct)
                    : action is "Print" or "Answers" && await access.PackageAsync(Id("id"), false, ct);
                break;
            case "Home": allowed = true; break;
            case "LearningObservations":
                if (c.ActionArguments.TryGetValue("classId", out var classValue) && classValue is long observationClass && observationClass <= 0
                    || c.ModelState.TryGetValue("classId", out var classState) && classState.Errors.Count != 0)
                { c.Result = new BadRequestResult(); return; }
                allowed = action == "Index" && !write && (Id("classId") == 0 || await access.ClassAsync(Id("classId"), ct))
                    || action == "Save" && write && await access.ClassAsync(Id("classId"), ct);
                break;
            case "StudentProgress":
                if (!c.ModelState.IsValid || Id("classId") <= 0 || Id("studentId") <= 0)
                { c.Result = new BadRequestResult(); return; }
                allowed = !write && action == "Index" && await access.ClassAsync(Id("classId"), ct);
                break;
            case "QuestionCsvImports":
                allowed = action is "Index" or "Template" or "Preview" or "Confirm";
                break;
            case "Teachers":
                allowed = actor.IsManager || !write && (action == "Index" || action == "Details" && Id("id") == actor.TeacherId); break;
            case "Classes":
                allowed = action == "Index" || action is "Create" or "Delete" || action is "Import" or "Template" or "Preview" or "Confirm"
                    ? (action == "Index" || actor.IsManager)
                    : action == "Details" && await access.ClassAsync(Id("id"), ct); break;
            case "Assessments":
                allowed = action == "Index" || await access.AssessmentAsync(action == "IndividualDiagnosis" ? Id("assessmentId") : Id("id"), false, ct); break;
            case "AssessmentFollowupPlans":
                allowed = action == "Edit" && await access.AssessmentAsync(Id("id"), write, ct); break;
            case "AssessmentImports": allowed = await access.AssessmentAsync(Id("id"), write, ct); break;
            case "StudentReports": allowed = await access.AssessmentAsync(Id("assessmentId"), false, ct); break;
            case "ParentComments":
                allowed = action is "Index" or "Save" or "Approve" or "Discard" or "Generate" or "AddObservation"
                    && await access.AssessmentAsync(Id("assessmentId"), write, ct); break;
            case "InterventionFollowups":
                allowed = await access.PackageAsync(Id("id"), write, ct); break;
            case "Interventions":
                if (action == "Index" || action == "Create" && !write) allowed = true;
                else if (action == "Create") allowed = c.ActionArguments.Values.OfType<CreateInterventionInput>().FirstOrDefault() is { } package && await access.AssessmentAsync(package.AssessmentId, true, ct);
                else allowed = await access.PackageAsync(Id("id"), write, ct);
                break;
            case "Authoring":
                var id = Id("id");
                if (action == "EditQuestion") allowed = actor.IsManager;
                else if (action is "Test" or "EditTest" or "Publish" or "CopyTest" || action == "FilterTest" && id > 0)
                    allowed = await access.TestAsync(id, action is "EditTest" or "Publish" or "FilterTest", ct);
                else if (action == "CreateAssessment" && write)
                    allowed = c.ActionArguments.Values.OfType<AssessmentCreateInput>().FirstOrDefault() is { } input
                        && (actor.IsManager || input.TeacherId == actor.TeacherId) && await access.ClassAsync(input.ClassId, ct);
                else allowed = action is "Index" or "WorkspaceTaxonomy" or "WorkspaceSkills" or "WorkspaceQuestions" or "Taxonomy" or "Questions" or "Question"
                    or "CreateQuestion" or "CopyQuestion" or "Tests" or "CreateTest" or "FilterTest" or "CreateAssessment";
                break;
            default: allowed = false; break;
        }
        if (!allowed)
        {
            c.HttpContext.Response.Headers.CacheControl = "no-store, private";
            c.Result = c.HttpContext.Request.Path.StartsWithSegments("/Authoring/Workspace") || c.HttpContext.Request.Headers.Accept.Any(a => a?.Contains("application/json") == true)
                ? new JsonResult(new { error = "Dữ liệu không tồn tại hoặc nằm ngoài phạm vi quyền." }) { StatusCode = 404 }
                : new ContentResult { Content = "Dữ liệu không tồn tại hoặc nằm ngoài phạm vi quyền. Liên hệ quản lý học vụ nếu cần hỗ trợ.", ContentType = "text/plain; charset=utf-8", StatusCode = 404 };
            return;
        }
        c.HttpContext.Response.Headers.CacheControl = "no-store, private";
        await next();
    }
}
