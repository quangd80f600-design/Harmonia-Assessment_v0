using System.Security.Claims;
using Harmonia.Web.Data;
using Harmonia.Web.Models.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace Harmonia.Web.Services.Security;
public sealed class HarmoniaClaimsFactory(UserManager<HarmoniaUser> users, RoleManager<IdentityRole> roles,
    IOptions<IdentityOptions> options, HarmoniaIdentityDbContext identity, IDbContextFactory<HarmoniaDbContext> factory) : UserClaimsPrincipalFactory<HarmoniaUser, IdentityRole>(users, roles, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(HarmoniaUser user)
    {
        var claims = await base.GenerateClaimsAsync(user);
        var state = await AccountEligibility.ReadAsync(user, UserManager, identity, factory);
        // Rebuild application-owned claims from current server data, never from stored arbitrary claims.
        foreach (var claim in claims.Claims.Where(c => c.Type is ClaimTypes.Role or HarmoniaRoles.TeacherIdClaim
            or AccountEligibility.TeacherLinkClaim or HarmoniaRoles.LoginClaim).ToArray()) claims.RemoveClaim(claim);
        if (state.Eligible)
        {
            foreach (var role in state.Roles) claims.AddClaim(new(ClaimTypes.Role, role));
            if (state.LinkedTeacherId is long linked) claims.AddClaim(new(AccountEligibility.TeacherLinkClaim, linked.ToString()));
            if (state.TeachingTeacherId is long teaching) claims.AddClaim(new(HarmoniaRoles.TeacherIdClaim, teaching.ToString()));
        }
        claims.AddClaim(new(HarmoniaRoles.LoginClaim, Guid.NewGuid().ToString("N")));
        return claims;
    }
}
public sealed class HarmoniaCookieEvents(UserManager<HarmoniaUser> users, HarmoniaIdentityDbContext identity,
    IDbContextFactory<HarmoniaDbContext> factory) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var user = context.Principal is null ? null : await users.GetUserAsync(context.Principal);
        var valid = !string.IsNullOrWhiteSpace(context.Principal?.FindFirstValue(HarmoniaRoles.LoginClaim)) && user is not null && user.IsActive && !await users.IsLockedOutAsync(user)
            && await users.GetSecurityStampAsync(user) == context.Principal!.FindFirstValue("AspNet.Identity.SecurityStamp");
        if (valid)
        {
            var state = await AccountEligibility.ReadAsync(user, users, identity, factory, context.HttpContext.RequestAborted);
            valid = state.Matches(context.Principal!);
        }
        if (!valid) { context.RejectPrincipal(); context.HttpContext.Session.Clear(); await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme); }
    }
    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context) => Redirect(context, 401);
    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context) => Redirect(context, 403);
    private static Task Redirect(RedirectContext<CookieAuthenticationOptions> context, int status)
    {
        if (!HttpMethods.IsGet(context.Request.Method) || context.Request.Path.StartsWithSegments("/Authoring/Workspace") || context.Request.Headers.Accept.Any(a => a?.Contains("application/json") == true))
            context.Response.StatusCode = status;
        else context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    }
}
