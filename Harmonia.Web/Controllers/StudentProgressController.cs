using Harmonia.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Harmonia.Web.Controllers;

public sealed class StudentProgressController(StudentProgressService progress) : Controller
{
    [HttpGet("/Classes/{classId:long}/Students/{studentId:long}/Progress", Name = "StudentProgress")]
    public async Task<IActionResult> Index([FromRoute] long classId, [FromRoute] long studentId,
        [FromQuery] long? subskillId, [FromQuery] int page = 1, CancellationToken ct = default)
    {
        Response.Headers.CacheControl = "no-store, private";
        // The shared layout's antiforgery form also sets cache headers during rendering.
        // Apply the private policy after those callbacks while retaining no-cache/no-store.
        Response.OnStarting(() =>
        {
            Response.Headers.CacheControl = "no-store, no-cache, private";
            return Task.CompletedTask;
        });
        if (!ModelState.IsValid || classId <= 0 || studentId <= 0 || subskillId is <= 0 || page <= 0)
            return BadRequest();
        var model = await progress.GetAsync(classId, studentId, subskillId, page, ct);
        return model is null ? NotFound() : View(model);
    }
}
