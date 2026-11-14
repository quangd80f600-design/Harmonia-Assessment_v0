using System.Security.Cryptography;
using Harmonia.Web.Services;
using Harmonia.Web.Services.Import;
using Harmonia.Web.Services.Security;
using Harmonia.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Harmonia.Web.Controllers;

[Route("Authoring/Questions/Import")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class QuestionCsvImportsController(QuestionCsvImportService service, AuthoringAccess access, CurrentActor actor) : Controller
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        Response.Headers.CacheControl = "no-store, private";
        if (!access.CanAccess(HttpContext) || !service.CanImport) context.Result = StatusCode(403);
        base.OnActionExecuting(context);
    }
    private string Owner()
    {
        var owner = HttpContext.Session.GetString("QuestionCsvOwner");
        if (owner is null)
        {
            owner = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            HttpContext.Session.SetString("QuestionCsvOwner", owner);
        }
        return actor.PreviewOwner(owner);
    }
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await service.GetPageAsync(ct));

    [HttpGet("Template")]
    public IActionResult Template() => File(QuestionCsvParser.Template(), "text/csv; charset=utf-8", "harmonia-question-template.csv");

    [HttpPost("Preview"), ValidateAntiForgeryToken]
    [RequestSizeLimit(QuestionCsvParser.MaxBytes + 65536)]
    [RequestFormLimits(MultipartBodyLengthLimit = QuestionCsvParser.MaxBytes + 65536)]
    public async Task<IActionResult> Preview(IFormFile? file, CancellationToken ct, [FromForm] string usagePurpose = "ForTest")
    {
        if (!ModelState.IsValid || file is null || file.Length is <= 0 or > QuestionCsvParser.MaxBytes
            || !string.Equals(Path.GetExtension(file.FileName), ".csv", StringComparison.OrdinalIgnoreCase))
            return await UploadError([new(null, "File", "Chọn file .csv UTF-8, tối đa 1 MB.")], ct);
        using var memory = new MemoryStream();
        await using var stream = file.OpenReadStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(buffer, ct)) > 0)
        {
            if (memory.Length + read > QuestionCsvParser.MaxBytes)
                return await UploadError([new(null, "File", "File vượt quá 1 MB.")], ct);
            await memory.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        var result = await service.PrepareAsync(memory.ToArray(), Owner(), ct, usagePurpose);
        return result.Preview is null ? await UploadError(result.Errors, ct, usagePurpose) : View(result.Preview);
    }

    [HttpPost("Confirm"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm([FromForm] string? token, [FromForm] bool confirmed, CancellationToken ct)
    {
        if (!ModelState.IsValid || token is null || token.Length != 64) return BadRequest();
        var owner = Owner();
        if (!confirmed)
        {
            var preview = service.GetPreview(token, owner);
            if (preview is null) return Unavailable();
            Response.StatusCode = 400;
            ViewData["ConfirmError"] = "Hãy xác nhận đã kiểm tra nội dung trước khi nhập.";
            return View("Preview", preview);
        }
        var result = await service.ConfirmAsync(token, owner, ct);
        if (result is null) return Unavailable();
        if (!result.Succeeded) Response.StatusCode = StatusCodes.Status409Conflict;
        return View("Result", result);
    }
    private async Task<IActionResult> UploadError(IReadOnlyList<ImportError> errors, CancellationToken ct, string usagePurpose = "ForTest")
    {
        Response.StatusCode = StatusCodes.Status400BadRequest;
        var page = await service.GetPageAsync(ct);
        return View("Index", page with { Errors = errors, UsagePurpose = usagePurpose });
    }
    private IActionResult Unavailable()
    {
        Response.StatusCode = StatusCodes.Status409Conflict;
        return View("Unavailable");
    }
}
