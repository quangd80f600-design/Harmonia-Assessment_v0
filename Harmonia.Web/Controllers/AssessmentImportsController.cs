using Harmonia.Web.Services.Security;
using System.Security.Cryptography;
using Harmonia.Web.Services.Import;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Harmonia.Web.Controllers;

[Route("Assessments/{id:long}/Import")]
public sealed class AssessmentImportsController(AssessmentImportService importer, CurrentActor? actor = null) : Controller
{
    private string Owner()
    {
        var owner = HttpContext.Session.GetString("ImportOwner");
        if (owner is null)
        {
            owner = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            HttpContext.Session.SetString("ImportOwner", owner);
        }
        return actor?.PreviewOwner(owner) ?? throw new UnauthorizedAccessException();
    }
    [HttpGet("")]
    public async Task<IActionResult> Index([FromRoute] long id, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || id <= 0) return BadRequest();
        var page = await importer.GetPageAsync(id, cancellationToken);
        if (page is null) return NotFound();
        return View(page);
    }
    [HttpGet("Template")]
    public async Task<IActionResult> Template([FromRoute] long id, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || id <= 0) return BadRequest();
        var template = await importer.GetTemplateAsync(id, cancellationToken);
        if (template is null) return NotFound();
        return File(template.Content, "text/csv; charset=utf-8", template.FileName);
    }
    [HttpPost("Preview"), ValidateAntiForgeryToken]
    [RequestSizeLimit(CsvResultParser.MaxBytes + 65536)]
    [RequestFormLimits(MultipartBodyLengthLimit = CsvResultParser.MaxBytes + 65536)]
    public async Task<IActionResult> Preview([FromRoute] long id, IFormFile? file, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || id <= 0) return BadRequest();
        var page = await importer.GetPageAsync(id, cancellationToken);
        if (page is null) return NotFound();
        if (!page.CanImport) return View("Index", page);
        if (file is null || file.Length is <= 0 or > CsvResultParser.MaxBytes
            || !string.Equals(Path.GetExtension(file.FileName), ".csv", StringComparison.OrdinalIgnoreCase))
            return View("Index", page with { Errors = new[] { new ViewModels.ImportError(null, "File", "Chọn một file .csv UTF-8, tối đa 1 MB.") } });
        using var memory = new MemoryStream();
        await file.CopyToAsync(memory, cancellationToken);
        var result = await importer.PrepareAsync(id, memory.ToArray(), file.FileName, Owner(), cancellationToken);
        if (result is null) return NotFound();
        if (result.Preview is null) return View("Index", result.Page);
        return View(result.Preview);
    }
    [HttpPost("Confirm"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm([FromRoute] long id, [FromForm] string? token, [FromForm] bool confirmed, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || id <= 0 || token is null || token.Length != 64) return BadRequest();
        var owner = Owner();
        if (!confirmed)
        {
            var preview = importer.GetPreview(id, token, owner);
            if (preview is null) return Expired(id);
            Response.StatusCode = StatusCodes.Status400BadRequest;
            ViewData["ConfirmError"] = "Hãy xác nhận file thuộc đúng đợt đánh giá trước khi lưu.";
            return View("Preview", preview);
        }
        try
        {
            var outcome = await importer.ConfirmAsync(id, token, owner, cancellationToken);
            if (outcome is null) return Expired(id);
            return View("Result", outcome);
        }
        catch (NpgsqlException)
        {
            TempData["ImportMessage"] = "Chưa kết nối được database. Kiểm tra S4 trước khi thử lại.";
            return RedirectToAction(nameof(Index), new { id });
        }
    }
    private IActionResult Expired(long id)
    {
        Response.StatusCode = StatusCodes.Status409Conflict;
        return View("PreviewUnavailable", id);
    }
}
