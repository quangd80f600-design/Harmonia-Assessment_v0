using Harmonia.Web.Services;
using Harmonia.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
namespace Harmonia.Web.Controllers;

[Route("Interventions/{id:long}/Followups")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class InterventionFollowupsController(InterventionFollowupService service) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(long id, CancellationToken ct)
    {
        if (!ModelState.IsValid || id <= 0) return BadRequest();
        var page = await service.GetAsync(id, ct);
        return page is null ? NotFound() : View(page);
    }
    [HttpPost(""), ValidateAntiForgeryToken]
    public async Task<IActionResult> Link(long id, LinkFollowupInput input, CancellationToken ct)
    {
        if (id <= 0) return BadRequest();
        if (ModelState.IsValid)
        {
            var result = await service.LinkAsync(id, input, ct);
            if (result.NotFound) return NotFound();
            if (result.Succeeded) { TempData["FollowupMessage"] = result.Message; return RedirectToAction(nameof(Index), new { id }); }
            ModelState.AddModelError("", result.Message);
        }
        var page = await service.GetAsync(id, ct);
        if (page is null) return NotFound();
        Response.StatusCode = 400;
        return View("Index", page);
    }
}
