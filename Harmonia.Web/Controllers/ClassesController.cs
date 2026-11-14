using Harmonia.Web.Services.Security;
using System.Security.Cryptography;
using Harmonia.Web.Services;
using Harmonia.Web.Services.Academic;
using Harmonia.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Harmonia.Web.Controllers;

[Route("Classes")]
public sealed class ClassesController(AcademicManagementService academic, StudentRosterImportService importer,
    CurrentActor? actor = null, AuthoringAccess? authoring = null) : Controller
{
    private string Owner()
    {
        var owner = HttpContext.Session.GetString("RosterImportOwner");
        if (owner is null) { owner = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)); HttpContext.Session.SetString("RosterImportOwner", owner); }
        return actor?.PreviewOwner(owner) ?? throw new UnauthorizedAccessException();
    }
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct, [FromQuery] bool mine = false)
    {
        if (!ModelState.IsValid) return BadRequest();
        return View(await academic.GetClassesAsync(ct, mine));
    }
    [HttpGet("Create")]
    public IActionResult Create() => academic.CanWrite ? View(new CreateClassInput()) : StatusCode(403);
    [HttpPost("Create"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateClassInput input, CancellationToken ct)
    {
        if (!academic.CanWrite) return StatusCode(403);
        if (!ModelState.IsValid) return View(input);
        try
        {
            var result = await academic.CreateAsync(input, ct);
            if (result.ClassId is long id) { TempData["AcademicMessage"] = "Đã tạo lớp. Có thể nhập danh sách học sinh."; return RedirectToAction(nameof(Details), new { id }); }
            foreach (var error in result.Errors) ModelState.AddModelError(error.Field, error.Message);
        }
        catch (Exception ex) when (ex is NpgsqlException or DbUpdateException)
        { ModelState.AddModelError("", "Chưa xác nhận được kết quả tạo lớp. Kiểm tra danh sách lớp trước khi thử lại."); }
        return View(input);
    }
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Details([FromRoute] long id, [FromQuery] DateOnly? asOf, CancellationToken ct)
    {
        if (!ModelState.IsValid || id <= 0) return BadRequest();
        var model = await academic.GetClassAsync(id, asOf, ct);
        return model is null ? NotFound() : View(model with {
            CanCreateAssessment = model.Class.Status == "Active" && authoring?.CanAccess(HttpContext) == true
        });
    }
    [HttpPost("{id:long}/Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete([FromRoute] long id, [FromForm] string? expectedName,
        [FromForm] bool confirmed, CancellationToken ct)
    {
        if (!academic.CanWrite) return StatusCode(403);
        if (!ModelState.IsValid || id <= 0 || !confirmed || string.IsNullOrWhiteSpace(expectedName) || expectedName.Length > 150)
            return BadRequest();
        try
        {
            var outcome = await academic.DeleteAsync(id, expectedName, ct);
            if (outcome.NotFound) return NotFound();
            TempData["AcademicMessage"] = outcome.Message;
            TempData["AcademicDeleteSucceeded"] = outcome.Deleted;
        }
        catch (Exception ex) when (ex is NpgsqlException or DbUpdateException)
        {
            // A lost commit acknowledgement is not proof of failure; do not claim rollback or retry automatically.
            TempData["AcademicMessage"] = "Chưa xác nhận được kết quả xóa. Tải lại danh sách lớp để kiểm tra trước khi thử lại.";
            TempData["AcademicDeleteSucceeded"] = false;
        }
        return RedirectToAction(nameof(Index));
    }
    [HttpGet("{id:long}/Import")]
    public async Task<IActionResult> Import([FromRoute] long id, CancellationToken ct)
    {
        if (!ModelState.IsValid || id <= 0) return BadRequest();
        var model = await importer.GetPageAsync(id, ct);
        return model is null ? NotFound() : View(model);
    }
    [HttpGet("{id:long}/Import/Template")]
    public async Task<IActionResult> Template([FromRoute] long id, CancellationToken ct)
    {
        if (!ModelState.IsValid || id <= 0) return BadRequest();
        var file = await importer.GetTemplateAsync(id, ct);
        return file is null ? NotFound() : File(file.Content, "text/csv; charset=utf-8", file.FileName);
    }
    [HttpPost("{id:long}/Import/Preview"), ValidateAntiForgeryToken]
    [RequestSizeLimit(StudentRosterParser.MaxBytes + 65536)]
    [RequestFormLimits(MultipartBodyLengthLimit = StudentRosterParser.MaxBytes + 65536)]
    public async Task<IActionResult> Preview([FromRoute] long id, IFormFile? file, CancellationToken ct)
    {
        if (!ModelState.IsValid || id <= 0) return BadRequest();
        var page = await importer.GetPageAsync(id, ct);
        if (page is null) return NotFound();
        if (!page.CanImport) return View("Import", page);
        if (file is null || file.Length is <= 0 or > StudentRosterParser.MaxBytes
            || !string.Equals(Path.GetExtension(file.FileName), ".csv", StringComparison.OrdinalIgnoreCase))
            return View("Import", page with { Errors = new[] { new ImportError(null, "File", "Chọn một file .csv UTF-8 tối đa 1 MB.") } });
        using var memory = new MemoryStream(); await file.CopyToAsync(memory, ct);
        var result = await importer.PrepareAsync(id, memory.ToArray(), file.FileName, Owner(), ct);
        if (result is null) return NotFound();
        return result.Preview is null ? View("Import", result.Page) : View(result.Preview);
    }
    [HttpPost("{id:long}/Import/Confirm"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm([FromRoute] long id, [FromForm] string? token, [FromForm] bool confirmed, CancellationToken ct)
    {
        if (!ModelState.IsValid || id <= 0 || token is null || token.Length != 64) return BadRequest();
        var owner = Owner();
        if (!confirmed)
        {
            var preview = importer.GetPreview(id, token, owner);
            if (preview is null) return Unavailable(id);
            Response.StatusCode = 400;
            ViewData["ConfirmError"] = "Hãy xác nhận đúng lớp và khoảng thời gian vào lớp trước khi lưu.";
            return View("Preview", preview);
        }
        try
        {
            var outcome = await importer.ConfirmAsync(id, token, owner, ct);
            return outcome is null ? Unavailable(id) : View("Result", outcome);
        }
        catch (Exception ex) when (ex is NpgsqlException or DbUpdateException)
        {
            TempData["AcademicMessage"] = "Chưa xác nhận được kết quả nhập. Kiểm tra danh sách lớp trước khi thử lại.";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
    private IActionResult Unavailable(long id)
    { Response.StatusCode = 409; return View("PreviewUnavailable", id); }
}
