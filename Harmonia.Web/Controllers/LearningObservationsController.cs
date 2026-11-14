using Harmonia.Web.Services;
using Harmonia.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Harmonia.Web.Controllers;

public sealed class LearningObservationsController(LearningObservationService observations) : Controller
{
    private void PrivateResponse() => Response.OnStarting(() =>
    { Response.Headers.CacheControl = "no-store, no-cache, private"; return Task.CompletedTask; });

    [HttpGet("/LearningObservations")]
    public async Task<IActionResult> Index([FromQuery] long? classId, [FromQuery] DateOnly? periodStart,
        [FromQuery] DateOnly? periodEnd, [FromQuery] string? authorId, [FromQuery] int page = 1, CancellationToken ct = default)
    {
        PrivateResponse();
        if (!observations.Enabled) return NotFound();
        if (!ModelState.IsValid || classId is <= 0 || page <= 0) return BadRequest("Lớp, ngày hoặc số trang không hợp lệ. Chọn lại từ giao diện ghi nhận.");
        var model = await observations.GetAsync(classId, periodStart ?? AcademicManagementService.Today, periodEnd ?? AcademicManagementService.Today, authorId, page, ct);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost("/LearningObservations/{classId:long}/Save"), ValidateAntiForgeryToken]
    [RequestSizeLimit(131072)]
    public async Task<IActionResult> Save([FromRoute] long classId, ObservationSaveInput input, CancellationToken ct)
    {
        PrivateResponse();
        if (!observations.Enabled) return NotFound();
        if (classId <= 0 || !ModelState.IsValid) return BadRequest("Dữ liệu gửi lên không hợp lệ. Tải lại trang rồi thử lại.");
        try
        {
            var result = await observations.SaveAsync(classId, input, ct);
            if (result.NotFound) return NotFound();
            if (result.Saved)
            {
                TempData["ObservationMessage"] = result.Message;
                return RedirectToAction(nameof(Index), new { classId, periodStart = input.PeriodStart?.ToString("yyyy-MM-dd"), periodEnd = input.PeriodEnd?.ToString("yyyy-MM-dd"), input.AuthorId, input.Page });
            }
            var model = await observations.GetAsync(classId, input.PeriodStart ?? AcademicManagementService.Today, input.PeriodEnd ?? AcademicManagementService.Today, input.AuthorId, Math.Max(1, input.Page), ct);
            if (model is null) return NotFound();
            var values = input.Rows.GroupBy(r => r.StudentId).ToDictionary(g => g.Key, g => g.First());
            model = model with { Errors = [result.Message], Students = model.Students.Select(s => values.TryGetValue(s.StudentId, out var posted) && s.CanEdit ? s with { Input = posted } : s).ToArray() };
            Response.StatusCode = result.Conflict ? 409 : 400;
            return View("Index", model);
        }
        catch (Exception ex) when (ex is NpgsqlException or DbUpdateException)
        { return StatusCode(503, "Chưa xác nhận được kết quả lưu. Tải lại danh sách để kiểm tra trước khi thử lại."); }
    }
}
