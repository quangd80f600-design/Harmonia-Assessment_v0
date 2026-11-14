using Harmonia.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Harmonia.Web.Data;

public partial class HarmoniaDbContext
{
    public DbSet<LearningObservation> LearningObservations => Set<LearningObservation>();

    private static void ConfigureLearningObservations(ModelBuilder builder)
    {
        var e = builder.Entity<LearningObservation>();
        e.ToTable("LearningObservation", "public");
        e.HasKey(x => x.LearningObservationId);
        e.Property(x => x.LearningObservationId).HasColumnName("LearningObservation_ID").UseIdentityAlwaysColumn();
        e.Property(x => x.StudentId).HasColumnName("Student_ID");
        e.Property(x => x.ClassId).HasColumnName("Class_ID");
        e.Property(x => x.CreatedByUserId).HasColumnName("CreatedByUser_ID");
        e.Property(x => x.UpdatedByUserId).HasColumnName("UpdatedByUser_ID");
        e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        e.Property(x => x.RowVersion).IsConcurrencyToken();
        e.Property(x => x.TaskCompletion).HasMaxLength(20);
        e.Property(x => x.Participation).HasMaxLength(20);
        e.Property(x => x.Preparation).HasMaxLength(20);
        e.Property(x => x.Cooperation).HasMaxLength(20);
        e.HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.NoAction);
        e.HasOne<Class>().WithMany().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.NoAction);
        // Actor FKs exist in PostgreSQL; Identity entities stay in their separate context.
        e.HasIndex(x => new { x.StudentId, x.ClassId, x.PeriodStart, x.PeriodEnd, x.CreatedByUserId }).IsUnique();
    }
}
