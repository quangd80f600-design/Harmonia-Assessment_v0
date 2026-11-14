using Harmonia.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Harmonia.Web.Data;

public partial class HarmoniaDbContext
{
    public DbSet<StudentReportComment> StudentReportComments => Set<StudentReportComment>();
    private static void ConfigureParentComments(ModelBuilder builder)
    {
        var e = builder.Entity<StudentReportComment>(); e.ToTable("StudentReportComment", "public");
        e.HasKey(x => x.StudentReportCommentId);
        e.Property(x => x.StudentReportCommentId).HasColumnName("StudentReportComment_ID").UseIdentityAlwaysColumn();
        e.Property(x => x.StudentId).HasColumnName("Student_ID");
        e.Property(x => x.AssessmentId).HasColumnName("Assessment_ID");
        e.Property(x => x.LearningObservationId).HasColumnName("LearningObservation_ID");
        e.Property(x => x.BaseApprovedCommentId).HasColumnName("BaseApprovedComment_ID");
        e.Property(x => x.CreatedByUserId).HasColumnName("CreatedByUser_ID");
        e.Property(x => x.UpdatedByUserId).HasColumnName("UpdatedByUser_ID");
        e.Property(x => x.ApprovedByUserId).HasColumnName("ApprovedByUser_ID");
        e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        e.Property(x => x.InputSnapshot).HasColumnType("jsonb");
        e.Property(x => x.InputFingerprint).HasColumnType("character(64)");
        e.Property(x => x.RowVersion).IsConcurrencyToken();
        e.HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.NoAction);
        e.HasOne<Assessment>().WithMany().HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.NoAction);
        e.HasOne<LearningObservation>().WithMany().HasForeignKey(x => x.LearningObservationId).OnDelete(DeleteBehavior.NoAction);
        e.HasOne<StudentReportComment>().WithMany().HasForeignKey(x => x.BaseApprovedCommentId).OnDelete(DeleteBehavior.NoAction);
        e.HasIndex(x => new { x.StudentId, x.AssessmentId, x.RevisionNumber }).IsUnique();
        e.HasIndex(x => new { x.StudentId, x.AssessmentId }).IsUnique().HasFilter("\"Status\" = 'Draft'").HasDatabaseName("UQ_StudentReportComment_Draft");
        // SQL owns both partial indexes on the same pair; no EF migration is used.
    }
}
