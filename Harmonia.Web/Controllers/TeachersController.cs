using Harmonia.Web.Models.Identity;
using Harmonia.Web.Services.Security;
using Harmonia.Web.Services;
using Harmonia.Web.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace Harmonia.Web.Controllers;
[Route("Teachers")]
public sealed class TeachersController(TeacherManagementService service):Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct, [FromQuery] long? classId = null)
    {
        if (!ModelState.IsValid || classId is <= 0) return BadRequest();
        if (classId.HasValue)
        {
            if (!User.IsInRole(HarmoniaRoles.Manager)) return NotFound();
            var cls = await service.GetAssignmentClassAsync(classId.Value, ct);
            if (cls is null || cls.Status != "Active") return NotFound();
            ViewData["AssignmentClass"] = cls;
        }
        return View(await service.ListAsync(ct));
    }
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Details(long id, CancellationToken ct, [FromQuery] long? classId = null)
    {
        if (!ModelState.IsValid || id <= 0 || classId is <= 0) return BadRequest();
        var page = await service.DetailsAsync(id, ct);
        if (page is null) return NotFound();
        if (classId.HasValue)
        {
            if (!User.IsInRole(HarmoniaRoles.Manager)) return NotFound();
            var cls = await service.GetAssignmentClassAsync(classId.Value, ct);
            if (cls is null || cls.Status != "Active") return NotFound();
            page = page with { Assignment = new() { ClassId = cls.ClassId, StartDate = AcademicManagementService.Today }, AssignmentClassContext = cls };
        }
        return View(page);
    }
    [Authorize(Roles=HarmoniaRoles.Manager),HttpGet("Create")]public IActionResult Create()=>View(new TeacherInput());
    [Authorize(Roles=HarmoniaRoles.Manager),HttpPost("Create"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TeacherInput input,CancellationToken ct)
    {
        if(ModelState.IsValid){try{var id=await service.CreateAsync(input,ct);if(id.HasValue)return RedirectToAction(nameof(Details),new{id});}catch(Exception ex)when(ex is DbUpdateException or NpgsqlException){}}
        ModelState.AddModelError("","Chưa tạo được hồ sơ. Kiểm tra trường bắt buộc, mã, email và số điện thoại trùng; tải lại danh sách trước khi thử lại.");return View(input);
    }
    [Authorize(Roles=HarmoniaRoles.Manager),HttpPost("{id:long}/Account"),ValidateAntiForgeryToken]
    public Task<IActionResult> Account(long id,TeacherAccountInput input,CancellationToken ct)=>Write(id,()=>service.CreateAccountAsync(id,input,ct));
    [Authorize(Roles=HarmoniaRoles.Manager),HttpPost("{id:long}/ResetPassword"),ValidateAntiForgeryToken]
    public Task<IActionResult> ResetPassword(long id,ResetTeacherPasswordInput input,CancellationToken ct)=>Write(id,()=>service.ResetAsync(id,input,ct));
    [Authorize(Roles=HarmoniaRoles.Manager),HttpPost("{id:long}/AccountStatus"),ValidateAntiForgeryToken]
    public Task<IActionResult> AccountStatus(long id,[FromForm, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired]bool active,CancellationToken ct)=>Write(id,()=>service.SetActiveAsync(id,active,ct));
    [Authorize(Roles=HarmoniaRoles.Manager),HttpPost("{id:long}/Assignments"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Assignments(long id, TeacherAssignmentInput input, CancellationToken ct,
        [FromForm] long? contextClassId = null)
    {
        if (id <= 0 || contextClassId is <= 0 || contextClassId.HasValue && contextClassId != input.ClassId) return BadRequest();
        // Date errors are validated before opening the write transaction and retain the user's form.
        if (AssignmentDates.Validate(input) is { } invalid) ModelState.AddModelError(invalid.Field ?? "", invalid.Message);
        if (ModelState.IsValid)
        {
            try
            {
                var result = await service.AssignWithResultAsync(id, input, ct);
                if (result.Succeeded)
                {
                    if (contextClassId.HasValue)
                    {
                        TempData["AcademicMessage"] = result.Message;
                        return RedirectToAction("Details", "Classes", new { id = contextClassId.Value });
                    }
                    TempData["TeacherMessage"] = result.Message;
                    return RedirectToAction(nameof(Details), new { id });
                }
                ModelState.AddModelError(result.Field ?? "", result.Message);
            }
            catch (Exception ex) when (ex is DbUpdateException or NpgsqlException)
            {
                ModelState.AddModelError("", "Chưa xác nhận được kết quả lưu. Kiểm tra lịch phân công trước khi thử lại; lớp và ngày đã nhập được giữ bên dưới.");
            }
        }
        var page = await service.DetailsAsync(id, ct);
        if (page is null) return NotFound();
        var context = contextClassId.HasValue ? await service.GetAssignmentClassAsync(contextClassId.Value, ct) : null;
        return View("Details", page with { Assignment = input, AssignmentClassContext = context });
    }
    [Authorize(Roles=HarmoniaRoles.Manager), HttpPost("{id:long}/LinkMyAccount"), ValidateAntiForgeryToken]
    public async Task<IActionResult> LinkMyAccount(long id, [FromForm] bool confirmed, CancellationToken ct)
    {
        if (!ModelState.IsValid || id <= 0 || !confirmed) return BadRequest();
        async Task<IActionResult> Reauthenticate(string message)
        {
            HttpContext.Session.Clear();
            await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            TempData["AccountMessage"] = message;
            return RedirectToAction("Login", "Account");
        }
        ManagerTeacherLinkResult result;
        try { result = await service.LinkOwnManagerAsync(id, ct); }
        catch (Exception ex) when (ex is DbUpdateException or NpgsqlException)
        {
            // Do not retry an uncertain commit or claim it failed atomically.
            return await Reauthenticate("Chưa xác nhận được kết quả liên kết. Đăng nhập lại và kiểm tra hồ sơ trước khi thử lại.");
        }
        if (!result.Succeeded)
        {
            TempData["TeacherMessage"] = result.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
        return await Reauthenticate(result.Message);
    }
    private async Task<IActionResult> Write(long id,Func<Task<bool>> action)
    {
        var saved=false;if(id>0&&ModelState.IsValid)try{saved=await action();}catch(Exception ex)when(ex is DbUpdateException or NpgsqlException){}
        TempData["TeacherMessage"]=saved?"Đã lưu thay đổi.":"Chưa xác nhận được kết quả lưu. Hãy tải lại hồ sơ và kiểm tra trạng thái tài khoản trước khi thử lại.";
        return RedirectToAction(nameof(Details),new{id});
    }
}
