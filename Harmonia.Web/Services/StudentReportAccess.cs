using System.Net;
using Microsoft.Extensions.Options;

namespace Harmonia.Web.Services;

public sealed class StudentReportOptions
{
    public bool Enabled { get; set; }
}

// Role/feature boundary; BusinessAuthorizationFilter additionally checks the Assessment resource.
public sealed class StudentReportAccess(IOptions<StudentReportOptions> options)
{
    public bool CanRead(HttpContext? context) => options.Value.Enabled && context?.User.Identity?.IsAuthenticated == true
        && (context.User.IsInRole("Teacher") || context.User.IsInRole("AcademicManager"));
}
