using System.Net;
using Microsoft.Extensions.Options;

namespace Harmonia.Web.Services;

public sealed class AuthoringOptions
{
    public bool Enabled { get; set; }
    public string DemoActorId { get; set; } = "harmonia-demo-authoring";
    public bool CanWrite => Enabled;
}
public sealed class AuthoringAccess(IOptions<AuthoringOptions> options)
{
    public bool CanAccess(HttpContext? context) => options.Value.Enabled && context?.User.Identity?.IsAuthenticated == true
        && (context.User.IsInRole("Teacher") || context.User.IsInRole("AcademicManager"));
}
