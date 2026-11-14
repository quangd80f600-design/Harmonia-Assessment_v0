using Harmonia.Web.Services;
using Harmonia.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Harmonia.Web.Controllers;

[Route("Interventions")]
public sealed class InterventionsController(InterventionManagementService service) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await service.GetIndexAsync(ct));

    [HttpGet("Create")]
    public async Task<IActionResult> Create(CancellationToken ct, [FromQuery] long? assessmentId = null)
    {
        if (!service.CanWrite) return StatusCode(403);
        if (!ModelState.IsValid || assessmentId is <= 0) return BadRequest();
        var page = await service.GetCreateAsync(new() { AssessmentId = assessmentId ?? 0 }, ct);
        if (assessmentId.HasValue && !page.Assessments.Any(a => a.AssessmentId == assessmentId)) return NotFound();
        return View(page);
    }

    [HttpPost("Create"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind(Prefix = "Input")] CreateInterventionInput input, CancellationToken ct)
    {
        if (!service.CanWrite) return StatusCode(403);
        if (ModelState.IsValid)
        {
            try
            {
                var result = await service.CreateAsync(input, ct);
                if (result.Succeeded) { TempData["InterventionMessage"] = result.Message; return RedirectToAction(nameof(Details), new { id = result.PackageId }); }
                ModelState.AddModelError("", result.Message);
            }
            catch (Exception ex) when (ex is NpgsqlException or DbUpdateException)
            { ModelState.AddModelError("", "Chưa xác nhận được kết quả tạo gói. Kiểm tra danh sách trước khi thử lại."); }
        }
        return View(await service.GetCreateAsync(input, ct));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Details([FromRoute] long id, CancellationToken ct)
    {
        if (id <= 0 || !ModelState.IsValid) return BadRequest();
        var model = await service.GetDetailsAsync(id, ct);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost("{id:long}/Resources"), ValidateAntiForgeryToken]
    public Task<IActionResult> Resources([FromRoute] long id, [FromForm] InterventionSelectionInput input, CancellationToken ct) =>
        Write(id, () => service.AttachAsync(id, input, ct), ct);

    [HttpPost("{id:long}/Students"), ValidateAntiForgeryToken]
    public Task<IActionResult> Students([FromRoute] long id, [FromForm] InterventionSelectionInput input, CancellationToken ct) =>
        Write(id, () => service.AssignAsync(id, input, ct), ct);

    [HttpPost("{id:long}/Students/{assignmentId:long}/Delivery"), ValidateAntiForgeryToken]
    public Task<IActionResult> Delivery([FromRoute] long id, [FromRoute] long assignmentId,
        [FromForm] InterventionDeliveryInput input, CancellationToken ct)
    {
        if (assignmentId <= 0) return Task.FromResult<IActionResult>(BadRequest());
        return Write(id, () => service.UpdateDeliveryAsync(id, assignmentId, input, ct), ct);
    }

    private async Task<IActionResult> Write(long id, Func<Task<InterventionWriteResult>> save, CancellationToken ct)
    {
        if (!service.CanWrite) return StatusCode(403);
        if (id <= 0) return BadRequest();
        if (ModelState.IsValid)
        {
            try
            {
                var result = await save();
                if (result.NotFound) return NotFound();
                if (result.Succeeded) { TempData["InterventionMessage"] = result.Message; return RedirectToAction(nameof(Details), new { id }); }
                ModelState.AddModelError("", result.Message);
            }
            catch (Exception ex) when (ex is NpgsqlException or DbUpdateException)
            { ModelState.AddModelError("", "Chưa xác nhận được kết quả lưu. Tải lại gói để kiểm tra trước khi thử lại."); }
        }
        var model = await service.GetDetailsAsync(id, ct);
        if (model is null) return NotFound();
        Response.StatusCode = 400;
        return View("Details", model);
    }
}
