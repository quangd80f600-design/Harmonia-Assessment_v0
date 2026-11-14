using Harmonia.Web.Services;
using Harmonia.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace Harmonia.Web.Controllers;

[Route("Assessments/{id:long}/FollowupPlan")]
public sealed class AssessmentFollowupPlansController(AssessmentFollowupPlanService service) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Edit([FromRoute] long id, CancellationToken ct)
    {
        if (!ModelState.IsValid || id <= 0) return BadRequest();
        var page = await service.GetAsync(id, ct);
        return page is null ? NotFound() : View(page);
    }

    [HttpPost(""), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit([FromRoute] long id, [Bind(Prefix = "Input")] AssessmentFollowupPlanInput input, CancellationToken ct)
    {
        if (id <= 0) return BadRequest();
        var page = await service.GetAsync(id, ct);
        if (page is null || !page.Summary.CanEdit) return NotFound();
        if (ModelState.TryGetValue("Input.PlannedFollowupDate", out var invalidDate) && invalidDate.Errors.Count > 0)
        {
            invalidDate.Errors.Clear();
            invalidDate.Errors.Add("Ngày dự kiến đánh giá lại chưa hợp lệ. Hãy chọn ngày theo biểu mẫu.");
        }
        if (ModelState.IsValid)
        {
            var result = await service.SaveAsync(id, input, ct);
            if (result.NotFound) return NotFound();
            if (result.Succeeded)
            {
                TempData["FollowupPlanMessage"] = result.Message;
                return RedirectToAction("Overview", "Assessments", new { id });
            }
            ModelState.AddModelError("", result.Message);
            if (result.Conflict) Response.StatusCode = 409;
            if (result.Uncertain) Response.StatusCode = 503;
        }
        // Keep the submitted version on an error; silently refreshing it would defeat stale-edit protection.
        return View(page with { Input = input });
    }
}
