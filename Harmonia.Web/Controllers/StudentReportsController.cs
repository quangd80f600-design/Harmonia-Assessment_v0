using Harmonia.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Harmonia.Web.Controllers;

public sealed class StudentReportsController(StudentReportService reports, StudentReportAccess access) : Controller
{
    [HttpGet("/Assessments/{assessmentId:long}/Students/{studentId:long}/Report", Name = "StudentReport")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Report([FromRoute] long assessmentId, [FromRoute] long studentId, CancellationToken ct)
    {
        Response.Headers["Cache-Control"] = "no-store, private";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow, noarchive";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Security-Policy"] = "default-src 'none'; style-src 'self'; font-src 'self'; img-src 'self'; script-src 'self'; base-uri 'none'; frame-ancestors 'none'";
        if (!access.CanRead(HttpContext)) return StatusCode(403, "Báo cáo chưa bật hoặc tài khoản không có quyền.");
        if (!ModelState.IsValid || assessmentId <= 0 || studentId <= 0) return BadRequest();
        var model = await reports.GetReportAsync(assessmentId, studentId, ct);
        return model is null ? NotFound() : View(model);
    }
}
