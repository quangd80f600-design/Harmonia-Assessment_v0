using System.Data;
using Harmonia.Web.Data;
using Harmonia.Web.Models.Entities;
using Harmonia.Web.Services.Security;
using Harmonia.Web.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Harmonia.Web.Services;

public sealed class InterventionFollowupService(IDbContextFactory<HarmoniaDbContext> factory,
    BusinessAccess access, CurrentActor actor, IOptions<InterventionManagementOptions> options,
    IOptions<WorksheetOptions>? worksheets = null)
{
    private static bool AssessmentAllowed(string status) => status is "Open" or "Completed";
    private static bool PackageWritable(string status) => status is "Draft" or "Published";
    private static FollowupAssessment Context(Assessment a) => new(a.AssessmentId, a.AssessmentName,
        a.AssessmentDate, a.Status, a.ClassId, a.TestId);

    // Reuse authoring snapshot validation, without applying today's bank/Test status to historical results.
    private static async Task<bool> ValidSnapshot(HarmoniaDbContext db, long testId, short grade, CancellationToken ct)
    {
        var snapshots = await db.QuestionInTests.AsNoTracking().Where(q => q.TestId == testId).OrderBy(q => q.QuestionNumber)
            .Select(q => new TestSnapshotRow(q.QuestionId, q.QuestionNumber, q.SnSquestionText, q.SnSoptionA,
                q.SnSoptionB, q.SnSoptionC, q.SnSoptionD, q.SnSanswerKey, q.SnSsubskillId,
                q.SnSsubskill.SubskillCode, q.SnSsubskill.SubskillName, q.SnSsubskill.KnowledgeArea.CurriculumUnit.Grade)).ToListAsync(ct);
        return AuthoringService.ValidateSnapshots(snapshots) is null && snapshots.All(q => q.Grade == grade);
    }
    public async Task<InterventionFollowupPage?> GetAsync(long id, CancellationToken ct = default)
    {
        if (id <= 0 || actor.Id is null) return null;
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", ct);
        // Scope queries use this transaction, not an independently read authorization snapshot.
        var package = await db.InterventionPackages.AsNoTracking()
            .Where(p => p.InterventionPackageId == id && db.Assessments.Where(access.AssessmentScope()).Any(a => a.AssessmentId == p.AssessmentId))
            .Select(p => new { p.PackageName, p.Status, p.AssessmentId }).SingleOrDefaultAsync(ct);
        if (package is null) return null;
        var source = await db.Assessments.AsNoTracking().SingleAsync(a => a.AssessmentId == package.AssessmentId, ct);
        var grade = await db.Classes.Where(c => c.ClassId == source.ClassId).Select(c => c.Grade).SingleAsync(ct);
        var canLink = options.Value.Enabled && PackageWritable(package.Status) && AssessmentAllowed(source.Status)
            && await db.Assessments.Where(access.AssessmentWriteScope()).AnyAsync(a => a.AssessmentId == source.AssessmentId, ct);
        var skills = await db.InterventionPackageResources.AsNoTracking().Where(r => r.InterventionPackageId == id)
            .Select(r => new { Id = r.InterventionResource.SubskillId, Code = r.InterventionResource.Subskill.SubskillCode,
                Name = r.InterventionResource.Subskill.SubskillName }).Distinct().OrderBy(s => s.Code).ToListAsync(ct);
        if (worksheets?.Value.Enabled == true)
        {
            var internalIds = db.InterventionPackageQuestions.Where(q => q.InterventionPackageId == id).Select(q => q.SnSsubskillId)
                .Union(db.InterventionPackageTheories.Where(t => t.InterventionPackageId == id).Select(t => t.SnSsubskillId));
            var internalSkills = await db.Subskills.AsNoTracking().Where(s => internalIds.Contains(s.SubskillId))
                .Select(s => new { Id = s.SubskillId, Code = s.SubskillCode, Name = s.SubskillName }).ToListAsync(ct);
            skills = skills.Concat(internalSkills).DistinctBy(s => s.Id).OrderBy(s => s.Code).ToList();
        }
        var assignments = await db.InterventionPackageStudents.AsNoTracking().Where(s => s.InterventionPackageId == id
            && s.Student.ClassMemberships.Any(m => m.ClassId == source.ClassId && m.StartDate <= source.AssessmentDate
                && (m.EndDate == null || m.EndDate > source.AssessmentDate)))
            .OrderBy(s => s.Student.StudentCode).Select(s => new { s.StudentId, s.Student.StudentCode,
                s.Student.FullName, s.DeliveredDate }).ToListAsync(ct); // No TeacherNote projection.
        var eligible = await db.Assessments.AsNoTracking().Where(access.AssessmentScope())
            .Where(a => a.ClassId == source.ClassId && a.AssessmentId != source.AssessmentId
                && a.AssessmentDate > source.AssessmentDate && (a.Status == "Open" || a.Status == "Completed"))
            .OrderBy(a => a.AssessmentDate).ThenBy(a => a.AssessmentId).ToListAsync(ct);
        var linkedIds = await db.InterventionPackageFollowups.AsNoTracking().Where(f => f.InterventionPackageId == id
            && db.Assessments.Where(access.AssessmentScope()).Any(a => a.AssessmentId == f.FollowupAssessmentId))
            .Select(f => f.FollowupAssessmentId).ToListAsync(ct);
        var valid = new List<FollowupAssessment>();
        foreach (var a in eligible) if (await ValidSnapshot(db, a.TestId, grade, ct)) valid.Add(Context(a));
        var followups = valid.Where(a => linkedIds.Contains(a.Id)).ToArray();
        var candidates = canLink ? valid.Where(a => !linkedIds.Contains(a.Id)).ToArray() : [];
        var assessments = followups.Prepend(Context(source)).ToArray();
        var studentIds = assignments.Select(s => s.StudentId).ToArray();
        var skillIds = skills.Select(s => s.Id).ToArray();
        var metrics = new Dictionary<(long Assessment, long Student, long Skill), FollowupMeasure>();
        // Aggregate each Assessment exactly once; no join to follow-up links or package resources can multiply Responses.
        foreach (var a in assessments)
        {
            var counts = await db.QuestionInTests.AsNoTracking().Where(q => q.TestId == a.TestId && skillIds.Contains(q.SnSsubskillId))
                .GroupBy(q => q.SnSsubskillId).Select(g => new { Skill = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Skill, x => x.Count, ct);
            var roster = await db.ClassMemberships.AsNoTracking().Where(m => m.ClassId == source.ClassId
                && studentIds.Contains(m.StudentId) && m.StartDate <= a.Date && (m.EndDate == null || m.EndDate > a.Date))
                .Select(m => m.StudentId).Distinct().ToArrayAsync(ct);
            var attempts = await db.Attempts.AsNoTracking().Where(at => at.AssessmentId == a.Id && studentIds.Contains(at.StudentId)
                && roster.Contains(at.StudentId)).Select(at => at.StudentId).ToArrayAsync(ct);
            var results = await db.Responses.AsNoTracking().Where(r => r.Attempt.AssessmentId == a.Id
                && studentIds.Contains(r.Attempt.StudentId) && roster.Contains(r.Attempt.StudentId)
                && r.QuestionInTest.TestId == a.TestId && skillIds.Contains(r.QuestionInTest.SnSsubskillId))
                .GroupBy(r => new { r.Attempt.StudentId, Skill = r.QuestionInTest.SnSsubskillId })
                .Select(g => new { g.Key.StudentId, g.Key.Skill, Total = g.Count(), Correct = g.Count(r => r.IsCorrect) }).ToListAsync(ct);
            foreach (var s in assignments) foreach (var skill in skills)
            {
                var r = results.SingleOrDefault(r => r.StudentId == s.StudentId && r.Skill == skill.Id);
                metrics[(a.Id, s.StudentId, skill.Id)] = new(counts.GetValueOrDefault(skill.Id), roster.Contains(s.StudentId),
                    attempts.Contains(s.StudentId), r?.Total ?? 0, r?.Correct ?? 0);
            }
        }
        var rows = assignments.SelectMany(s => skills.Select(skill =>
        {
            var baseline = metrics[(source.AssessmentId, s.StudentId, skill.Id)];
            return new FollowupStudentSkill(s.StudentId, s.StudentCode, s.FullName, new(skill.Id, skill.Code, skill.Name), baseline,
                followups.Select(a => new FollowupComparison(a, metrics[(a.Id, s.StudentId, skill.Id)], s.DeliveredDate, baseline)).ToArray());
        })).ToArray();
        await tx.CommitAsync(ct);
        return new(id, package.PackageName, package.Status, Context(source), canLink,
            candidates, followups, rows, assignments.Count, skills.Count);
    }

    public async Task<FollowupWriteResult> LinkAsync(long id, LinkFollowupInput input, CancellationToken ct = default)
    {
        if (actor.Id is null || !options.Value.Enabled) return new(false, "Chức năng ghi can thiệp chưa được bật.");
        if (id <= 0 || InterventionManagementService.Validate(input) is string) return new(false, "Chọn gói và đợt đánh giá theo dõi hợp lệ.");
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var commitStarted = false;
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"InterventionPackage\" WHERE \"InterventionPackage_ID\"={id} FOR UPDATE", ct);
            var package = await db.InterventionPackages.SingleOrDefaultAsync(p => p.InterventionPackageId == id, ct);
            if (package is null || !await db.Assessments.Where(access.AssessmentWriteScope()).AnyAsync(a => a.AssessmentId == package.AssessmentId, ct))
                return new(false, "Không tìm thấy gói hoặc không có quyền ghi.", true);
            // Stable ordering also protects against reverse-link deadlocks between packages.
            var lockIds = new[] { package.AssessmentId, input.AssessmentId }.Distinct().Order().ToArray();
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Assessment\" WHERE \"Assessment_ID\" = ANY({lockIds}) ORDER BY \"Assessment_ID\" FOR SHARE", ct);
            var source = await db.Assessments.AsNoTracking().SingleAsync(a => a.AssessmentId == package.AssessmentId, ct);
            if (!actor.IsManager && actor.TeacherId is long teacher)
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"TeacherClassAssignment\" WHERE \"Class_ID\"={source.ClassId} AND \"Teacher_ID\"={teacher} ORDER BY \"TeacherClassAssignment_ID\" FOR SHARE", ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Class\" WHERE \"Class_ID\"={source.ClassId} FOR SHARE", ct);
            if (!await db.Assessments.Where(access.AssessmentWriteScope()).AnyAsync(a => a.AssessmentId == source.AssessmentId, ct))
                return new(false, "Không còn quyền ghi gói can thiệp.", true);
            var after = await db.Assessments.AsNoTracking().Where(access.AssessmentScope()).SingleOrDefaultAsync(a => a.AssessmentId == input.AssessmentId, ct);
            if (after is null) return new(false, "Đợt đánh giá không tồn tại hoặc ngoài phạm vi quyền.", true);
            if (!PackageWritable(package.Status) || !AssessmentAllowed(source.Status)) return new(false, "Gói hoặc đợt nguồn không còn được phép ghi theo quy tắc S9.");
            if (after.AssessmentId == source.AssessmentId || after.ClassId != source.ClassId || after.AssessmentDate <= source.AssessmentDate || !AssessmentAllowed(after.Status))
                return new(false, "Chọn đợt Open/Completed cùng lớp, khác đợt nguồn và có ngày sau đợt nguồn.");
            var testIds = new[] { source.TestId, after.TestId }.Distinct().Order().ToArray();
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Test\" WHERE \"Test_ID\" = ANY({testIds}) ORDER BY \"Test_ID\" FOR SHARE", ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"QuestionInTest\" WHERE \"Test_ID\" = ANY({testIds}) ORDER BY \"QuestionInTest_ID\" FOR SHARE", ct);
            var grade = await db.Classes.Where(c => c.ClassId == source.ClassId).Select(c => c.Grade).SingleAsync(ct);
            if (!await ValidSnapshot(db, source.TestId, grade, ct) || !await ValidSnapshot(db, after.TestId, grade, ct))
                return new(false, "Snapshot đề đánh giá không hợp lệ hoặc không cùng khối. Không liên kết.");
            if (await db.InterventionPackageFollowups.AnyAsync(f => f.InterventionPackageId == id && f.FollowupAssessmentId == input.AssessmentId, ct))
                return new(false, "Đợt đánh giá đã được liên kết; không tạo liên kết trùng.");
            db.InterventionPackageFollowups.Add(new() { InterventionPackageId = id, FollowupAssessmentId = input.AssessmentId });
            await db.SaveChangesAsync(ct);commitStarted = true;await tx.CommitAsync(ct);
            return new(true, "Đã liên kết đợt đánh giá theo dõi.");
        }
        catch (Exception ex) when (ex is DbUpdateException or NpgsqlException or OperationCanceledException)
        {
            try { await tx.RollbackAsync(CancellationToken.None); } catch (Exception rollback) when (rollback is NpgsqlException or InvalidOperationException) { }
            return new(false, commitStarted ? "Chưa xác nhận được kết quả lưu. Tải lại để kiểm tra trước khi thử lại."
                : "Không lưu được liên kết; dữ liệu có thể vừa thay đổi. Tải lại để kiểm tra, không tự gửi lại.");
        }
    }
}
