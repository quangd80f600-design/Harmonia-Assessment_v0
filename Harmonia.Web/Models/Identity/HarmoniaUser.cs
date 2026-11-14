using Microsoft.AspNetCore.Identity;
namespace Harmonia.Web.Models.Identity;
public sealed class HarmoniaUser : IdentityUser
{
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; } = true;
}
public sealed class TeacherUserLink
{
    public string UserId { get; set; } = "";
    public long TeacherId { get; set; }
    public HarmoniaUser User { get; set; } = null!;
}
public static class HarmoniaRoles
{
    public const string Teacher = "Teacher";
    public const string Manager = "AcademicManager";
    public const string TeacherIdClaim = "harmonia:teacher";
    public const string LoginClaim = "harmonia:login";
}
