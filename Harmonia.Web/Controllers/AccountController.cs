using Harmonia.Web.Data;
using Harmonia.Web.Services.Security;
using Harmonia.Web.Models.Identity;
using Harmonia.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Harmonia.Web.Controllers;
[ResponseCache(NoStore=true, Location=ResponseCacheLocation.None)]
public sealed class AccountController(SignInManager<HarmoniaUser> signIn, UserManager<HarmoniaUser> users,
    HarmoniaIdentityDbContext identity, IDbContextFactory<HarmoniaDbContext> factory) : Controller
{
    [AllowAnonymous, HttpGet]
    public IActionResult Login(string? returnUrl = null) => View(new LoginInput { ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null });
    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginInput input)
    {
        if (!ModelState.IsValid) { Scrub(); return View(input); }
        var user = await users.FindByNameAsync(input.UserName.Trim());
        var state = await AccountEligibility.ReadAsync(user, users, identity, factory, HttpContext.RequestAborted);
        var eligible = state.Eligible;
        var result = eligible ? await signIn.CheckPasswordSignInAsync(user!, input.Password, lockoutOnFailure:true) : Microsoft.AspNetCore.Identity.SignInResult.Failed;
        if (!result.Succeeded) { ModelState.AddModelError("", "Không đăng nhập được. Kiểm tra thông tin hoặc liên hệ quản lý."); Scrub(); input.Password = ""; return View(input); }
        HttpContext.Session.Clear(); await signIn.SignInAsync(user!, false);
        return user!.MustChangePassword ? RedirectToAction(nameof(ChangePassword)) : LocalRedirect(Url.IsLocalUrl(input.ReturnUrl) ? input.ReturnUrl! : "/Assessments");
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var user = await users.GetUserAsync(User);
        // Revoke copied cookies too, not merely the cookie in this browser. V0 logs out every session of this account.
        if (user is not null && !(await users.UpdateSecurityStampAsync(user)).Succeeded)
            return StatusCode(503, "Chưa xác nhận được việc đăng xuất. Hãy kiểm tra trạng thái đăng nhập trước khi thử lại.");
        HttpContext.Session.Clear(); await signIn.SignOutAsync(); return RedirectToAction(nameof(Login));
    }
    [HttpGet] public IActionResult ChangePassword() => View(new PasswordInput());
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(PasswordInput input)
    {
        if (ModelState.IsValid)
        {
            var user = await users.GetUserAsync(User); if (user is null) return Challenge();
            await using var tx = await identity.Database.BeginTransactionAsync();
            var result = await users.ChangePasswordAsync(user, input.CurrentPassword, input.NewPassword);
            if (result.Succeeded) {
                user.MustChangePassword = false; result = await users.UpdateAsync(user);
                if (result.Succeeded) { await tx.CommitAsync(); HttpContext.Session.Clear(); await signIn.RefreshSignInAsync(user); return RedirectToAction("Index", "Assessments"); }
            }
            ModelState.AddModelError("", "Không đổi được mật khẩu. Kiểm tra mật khẩu hiện tại và yêu cầu mật khẩu mới.");
        }
        Scrub(); return View(new PasswordInput());
    }
    [HttpGet] public IActionResult AccessDenied() { Response.StatusCode=403; return View(); }
    private void Scrub() { foreach(var key in ModelState.Keys.Where(k=>k.Contains("Password",StringComparison.OrdinalIgnoreCase)).ToArray()) ModelState.Remove(key); }
}
