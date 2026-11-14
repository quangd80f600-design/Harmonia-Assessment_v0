using System.Data;
using Harmonia.Web.Data;
using Harmonia.Web.Services.Security;
using Harmonia.Web.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Harmonia.Web.Services;

public sealed class StudentProgressService(IDbContextFactory<HarmoniaDbContext> factory,
    BusinessAccess access, CurrentActor actor)
{
    public async Task<StudentProgressViewModel?> GetAsync(long classId, long studentId,
        long? subskillId = null, int page = 1, CancellationToken ct = default)
    {
        if (actor.Id is null || classId <= 0 || studentId <= 0 || subskillId is <= 0 || page <= 0) return null;
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", ct);
        // All scope and result queries share this read snapshot, including current assignments.
        var className = await db.Classes.AsNoTracking().Where(access.ClassScope())
            .Where(c => c.ClassId == classId).Select(c => c.ClassName).SingleOrDefaultAsync(ct);
        if (className is null) return null;
        var student = await db.Students.AsNoTracking().Where(s => s.StudentId == studentId
            && s.ClassMemberships.Any(m => m.ClassId == classId))
            .Select(s => new { s.StudentCode, s.FullName }).SingleOrDefaultAsync(ct);
        if (student is null) return null;

        var eligible = db.Assessments.AsNoTracking().Where(access.AssessmentScope())
            .Where(a => a.ClassId == classId && db.ClassMemberships.Any(m => m.StudentId == studentId
                && m.ClassId == classId && m.StartDate <= a.AssessmentDate
                && (m.EndDate == null || m.EndDate > a.AssessmentDate)));
        var total = await eligible.CountAsync(ct);
        var skillLabels = await db.QuestionInTests.AsNoTracking()
            .Where(q => eligible.Any(a => a.TestId == q.TestId))
            .Select(q => new { Id = q.SnSsubskillId, Code = q.SnSsubskill.SubskillCode, Name = q.SnSsubskill.SubskillName })
            .Distinct().OrderBy(s => s.Code).ThenBy(s => s.Id).ToListAsync(ct);
        var skills = skillLabels.Select(s => new ProgressSubskill(s.Id, s.Code, s.Name)).ToList();
        if (subskillId.HasValue && !skills.Any(s => s.Id == subskillId)) return null;
        // Avoid integer overflow for untrusted page numbers; beyond-last pages remain empty.
        var offset = ((long)page - 1) * StudentProgressViewModel.PageSize;
        var assessments = offset >= total ? [] : await eligible
            .OrderByDescending(a => a.AssessmentDate).ThenByDescending(a => a.AssessmentId)
            .Skip((int)offset).Take(StudentProgressViewModel.PageSize)
            .Select(a => new { Id = a.AssessmentId, a.AssessmentName, a.AssessmentDate, a.TestId, a.Test.TestName })
            .ToListAsync(ct);
        var rows = new List<ProgressAssessment>();
        if (assessments.Count > 0)
        {
            var ids = assessments.Select(a => a.Id).ToArray();
            var tests = assessments.Select(a => a.TestId).Distinct().ToArray();
            var attempts = await db.Attempts.AsNoTracking().Where(a => a.StudentId == studentId && ids.Contains(a.AssessmentId))
                .Select(a => new { a.AssessmentId, a.Score }).ToDictionaryAsync(a => a.AssessmentId, ct);
            var counts = await db.QuestionInTests.AsNoTracking().Where(q => tests.Contains(q.TestId))
                .GroupBy(q => new { q.TestId, q.SnSsubskillId })
                .Select(g => new { g.Key.TestId, Skill = g.Key.SnSsubskillId, Count = g.Count() }).ToListAsync(ct);
            var answers = await db.Responses.AsNoTracking()
                .Where(r => r.Attempt.StudentId == studentId && ids.Contains(r.Attempt.AssessmentId)
                    && r.QuestionInTest.TestId == r.Attempt.Assessment.TestId)
                .GroupBy(r => new { r.Attempt.AssessmentId, r.QuestionInTest.SnSsubskillId })
                .Select(g => new { g.Key.AssessmentId, Skill = g.Key.SnSsubskillId,
                    Total = g.Count(), Correct = g.Count(r => r.IsCorrect) }).ToListAsync(ct);
            foreach (var a in assessments)
            {
                var hasAttempt = attempts.TryGetValue(a.Id, out var attempt);
                ProgressMeasure Measure(long? skill) => new(hasAttempt,
                    counts.Where(c => c.TestId == a.TestId && (!skill.HasValue || c.Skill == skill)).Sum(c => c.Count),
                    answers.Where(r => r.AssessmentId == a.Id && (!skill.HasValue || r.Skill == skill)).Sum(r => r.Total),
                    answers.Where(r => r.AssessmentId == a.Id && (!skill.HasValue || r.Skill == skill)).Sum(r => r.Correct));
                rows.Add(new(a.Id, a.AssessmentName, a.AssessmentDate, a.TestName, attempt?.Score,
                    Measure(null), subskillId.HasValue ? Measure(subskillId) : null));
            }
        }
        return new(classId, className, studentId, student.StudentCode, student.FullName,
            skills, subskillId, page, total, rows);
    }
}
