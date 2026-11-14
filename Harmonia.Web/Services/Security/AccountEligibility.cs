using System.Security.Claims;
using Harmonia.Web.Data;
using Harmonia.Web.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Harmonia.Web.Services.Security;

// One policy for login, claims creation and every authenticated request.
public static class AccountEligibility
{
    public const string TeacherLinkClaim = "harmonia:teacher-link";

    public static bool SupportedRoles(IEnumerable<string> roles)
    {
        var values = roles.ToArray();
        return values.Length is 1 or 2 && values.Distinct(StringComparer.Ordinal).Count() == values.Length
            && values.All(r => r is HarmoniaRoles.Manager or HarmoniaRoles.Teacher);
    }

    public static async Task<AccountAccessState> ReadAsync(HarmoniaUser? user, UserManager<HarmoniaUser> users,
        HarmoniaIdentityDbContext identity, IDbContextFactory<HarmoniaDbContext> factory, CancellationToken ct = default)
    {
        if (user is null) return new(false, [], null, null);
        var roles = (await users.GetRolesAsync(user)).Order(StringComparer.Ordinal).ToArray();
        var linked = await identity.TeacherUserLinks.AsNoTracking().Where(l => l.UserId == user.Id)
            .Select(l => (long?)l.TeacherId).SingleOrDefaultAsync(ct);
        bool exists = false, active = false;
        if (linked.HasValue)
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            var status = await db.Teachers.AsNoTracking().Where(t => t.TeacherId == linked)
                .Select(t => t.Status).SingleOrDefaultAsync(ct);
            exists = status is not null; active = status == "Active";
        }
        var teacher = roles.Contains(HarmoniaRoles.Teacher);
        var manager = roles.Contains(HarmoniaRoles.Manager);
        var eligible = user.IsActive && !await users.IsLockedOutAsync(user) && SupportedRoles(roles)
            && (!teacher || linked.HasValue) && (!linked.HasValue || exists) && (manager || active);
        return new(eligible, roles, linked, eligible && teacher && active ? linked : null);
    }
}

public sealed record AccountAccessState(bool Eligible, string[] Roles, long? LinkedTeacherId, long? TeachingTeacherId)
{
    public bool Matches(ClaimsPrincipal principal)
    {
        var roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).Order(StringComparer.Ordinal).ToArray();
        bool ClaimMatches(string type, long? expected)
        {
            var values = principal.FindAll(type).Select(c => c.Value).ToArray();
            return expected.HasValue ? values.Length == 1 && values[0] == expected.Value.ToString()
                : values.Length == 0;
        }
        return Eligible && roles.SequenceEqual(Roles)
            && ClaimMatches(AccountEligibility.TeacherLinkClaim, LinkedTeacherId)
            && ClaimMatches(HarmoniaRoles.TeacherIdClaim, TeachingTeacherId);
    }
}
