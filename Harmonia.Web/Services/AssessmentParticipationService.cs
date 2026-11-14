using Harmonia.Web.Data;
using Harmonia.Web.Models.Entities;
using Harmonia.Web.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Harmonia.Web.Services;

public sealed class AssessmentParticipationService(HarmoniaDbContext db)
{
    public async Task<ResultCompletion?> GetCompletionAsync(long assessmentId,
        CancellationToken cancellationToken = default)
    {
        // Reuse S4's unique eligible student roster and its exact temporal/Attempt rules.
        var overview = await GetOverviewAsync(assessmentId, cancellationToken: cancellationToken);
        return overview is null ? null : new(overview.ExpectedStudents, overview.RecordedStudents);
    }

    public async Task<AssessmentParticipationViewModel?> GetOverviewAsync(long assessmentId,
        ParticipationStatus status = ParticipationStatus.All, CancellationToken cancellationToken = default)
    {
        // Fetch the Assessment independently: an empty roster must not hide it.
        var context = await db.Assessments.AsNoTracking().Where(a => a.AssessmentId == assessmentId)
            .Select(a => new
            {
                a.ClassId,
                Header = new AssessmentHeader(a.AssessmentId, a.AssessmentName,
                    a.AssessmentDate, a.Class.ClassName, a.Test.TestName)
            }).SingleOrDefaultAsync(cancellationToken);
        if (context is null) return null;

        var students = await BuildStudentQuery(db.Students.AsNoTracking(), db.Attempts.AsNoTracking(),
            db.ImportBatches.AsNoTracking(), assessmentId, context.ClassId, context.Header.AssessmentDate)
            .ToListAsync(cancellationToken);
        return new(context.Header, students, status);
    }

    internal static IQueryable<ParticipationStudentRow> BuildStudentQuery(IQueryable<Student> students,
        IQueryable<Attempt> attempts, IQueryable<ImportBatch> batches,
        long assessmentId, long classId, DateOnly assessmentDate)
    {
        // EXISTS rather than joining memberships prevents duplicate student rows.
        var eligible = students.Where(s => s.ClassMemberships.Any(cm => cm.ClassId == classId
            && cm.StartDate <= assessmentDate && (cm.EndDate == null || cm.EndDate > assessmentDate)));

        // UNIQUE(Student_ID, Assessment_ID) guarantees at most one Attempt per student.
        return from student in eligible
               join attempt in attempts.Where(a => a.AssessmentId == assessmentId)
                   on student.StudentId equals attempt.StudentId into studentAttempts
               from attempt in studentAttempts.DefaultIfEmpty()
               join batch in batches on (attempt == null ? (long?)null : attempt.ImportBatchId)
                   equals (long?)batch.ImportBatchId into attemptBatches
               from batch in attemptBatches.DefaultIfEmpty()
               orderby attempt != null, student.StudentCode
               select new ParticipationStudentRow(student.StudentId, student.StudentCode, student.FullName,
                   attempt == null ? null : attempt.AttemptId,
                   attempt == null ? null : attempt.Score,
                   attempt == null ? null : attempt.ImportBatchId,
                   batch == null ? null : batch.OriginalFileName,
                   batch == null ? null : batch.Status);
    }
}
