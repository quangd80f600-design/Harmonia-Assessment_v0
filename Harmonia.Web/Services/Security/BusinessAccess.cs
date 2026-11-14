using System.Linq.Expressions;
using Harmonia.Web.Data;
using Harmonia.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
namespace Harmonia.Web.Services.Security;
public sealed class BusinessAccess(IDbContextFactory<HarmoniaDbContext> factory, CurrentActor actor)
{
    public Expression<Func<Class, bool>> ClassScope()
    {
        var all = actor.IsManager; var teacher = actor.TeacherId; var today = AcademicManagementService.Today;
        return c => all || teacher.HasValue && c.TeacherClassAssignments.Any(a => a.TeacherId == teacher && a.StartDate <= today && (a.EndDate == null || a.EndDate > today));
    }
    public Expression<Func<Assessment, bool>> AssessmentScope()
    {
        var all = actor.IsManager; var teacher = actor.TeacherId; var today = AcademicManagementService.Today;
        return a => all || teacher.HasValue && a.Class.TeacherClassAssignments.Any(t => t.TeacherId == teacher && t.StartDate <= today && (t.EndDate == null || t.EndDate > today));
    }
    public Expression<Func<Test, bool>> TestScope()
    {
        var all = actor.IsManager; var user = actor.Id;
        return t => all || user != null && (t.Status == "Published" || t.Status == "Archived" || t.Status == "Draft" && t.CreatedByUserId == user);
    }
    public Expression<Func<Assessment, bool>> AssessmentWriteScope()
    {
        var all = actor.IsManager; var teacher = actor.TeacherId; var today = AcademicManagementService.Today;
        return a => all || teacher.HasValue
            && a.Class.TeacherClassAssignments.Any(t => t.TeacherId == teacher && t.StartDate <= today && (t.EndDate == null || t.EndDate > today))
            && a.Class.TeacherClassAssignments.Any(t => t.TeacherId == teacher && t.StartDate <= a.AssessmentDate && (t.EndDate == null || t.EndDate > a.AssessmentDate));
    }
    public async Task<bool> ClassAsync(long id, CancellationToken ct)
    { await using var db = await factory.CreateDbContextAsync(ct); return await db.Classes.Where(ClassScope()).AnyAsync(c => c.ClassId == id, ct); }
    public async Task<bool> AssessmentAsync(long id, bool write, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var q = db.Assessments.Where(write ? AssessmentWriteScope() : AssessmentScope()).Where(a => a.AssessmentId == id);
        return await q.AnyAsync(ct);
    }
    public async Task<bool> PackageAsync(long id, bool write, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var source = await db.InterventionPackages.Where(p => p.InterventionPackageId == id).Select(p => (long?)p.AssessmentId).SingleOrDefaultAsync(ct);
        return source.HasValue && await AssessmentAsync(source.Value, write, ct);
    }
    public async Task<bool> TestAsync(long id, bool manage, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var q = db.Tests.Where(TestScope()).Where(t => t.TestId == id);
        if (manage && !actor.IsManager) q = q.Where(t => t.Status == "Draft" && t.CreatedByUserId == actor.Id);
        return await q.AnyAsync(ct);
    }
}
