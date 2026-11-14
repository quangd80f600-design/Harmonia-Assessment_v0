using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Harmonia.Web.Models;

namespace Harmonia.Web.Controllers;

public class HomeController(Harmonia.Web.Services.DashboardService dashboard) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(long? classId, long? assessmentId, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || classId is <= 0 || assessmentId is <= 0) return BadRequest();
        var page = await dashboard.GetAsync(new(classId, assessmentId), cancellationToken);
        return page is null ? NotFound() : View(page);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
