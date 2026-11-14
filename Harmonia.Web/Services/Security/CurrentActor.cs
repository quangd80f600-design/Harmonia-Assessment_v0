using System.Security.Claims;
using Harmonia.Web.Models.Identity;
namespace Harmonia.Web.Services.Security;
public sealed class CurrentActor(IHttpContextAccessor http)
{
    public ClaimsPrincipal User => http.HttpContext?.User ?? new(new ClaimsIdentity());
    public string? Id => User.Identity?.IsAuthenticated == true ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null;
    public bool IsManager => Id is not null && User.IsInRole(HarmoniaRoles.Manager);
    public long? TeacherId => User.IsInRole(HarmoniaRoles.Teacher) && long.TryParse(User.FindFirstValue(HarmoniaRoles.TeacherIdClaim), out var id) && id > 0 ? id : null;
    public bool CanTeach => Id is not null && TeacherId.HasValue;
    public string RequiredId => Id ?? throw new UnauthorizedAccessException("Cần đăng nhập.");
    public string PreviewOwner(string browserOwner) => RequiredId + ":" + (User.FindFirstValue(HarmoniaRoles.LoginClaim) ?? throw new UnauthorizedAccessException()) + ":" + browserOwner;
}
