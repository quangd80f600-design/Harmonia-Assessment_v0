using Harmonia.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Harmonia.Web.Data;

public partial class HarmoniaDbContext
{
    public DbSet<InterventionTheory> InterventionTheories => Set<InterventionTheory>();
    public DbSet<InterventionPackageTheory> InterventionPackageTheories => Set<InterventionPackageTheory>();
    public DbSet<InterventionPackageQuestion> InterventionPackageQuestions => Set<InterventionPackageQuestion>();
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        ConfigureLearningObservations(modelBuilder);
        ConfigureParentComments(modelBuilder);
        var theory = modelBuilder.Entity<InterventionTheory>();
        theory.ToTable("InterventionTheory", "public");
        theory.HasKey(x => x.InterventionTheoryId);
        theory.Property(x => x.InterventionTheoryId).HasColumnName("InterventionTheory_ID").UseIdentityAlwaysColumn();
        theory.Property(x => x.SubskillId).HasColumnName("Subskill_ID");
        theory.HasOne<Subskill>().WithMany().HasForeignKey(x => x.SubskillId).OnDelete(DeleteBehavior.NoAction);
        var t = modelBuilder.Entity<InterventionPackageTheory>();
        t.ToTable("InterventionPackageTheory", "public"); t.HasKey(x => x.InterventionPackageTheoryId);
        t.Property(x => x.InterventionPackageTheoryId).HasColumnName("InterventionPackageTheory_ID").UseIdentityAlwaysColumn();
        t.Property(x => x.InterventionPackageId).HasColumnName("InterventionPackage_ID");
        t.Property(x => x.InterventionTheoryId).HasColumnName("InterventionTheory_ID");
        t.Property(x => x.SnStitle).HasColumnName("SnSTitle"); t.Property(x => x.SnScontent).HasColumnName("SnSContent");
        t.Property(x => x.SnSsubskillId).HasColumnName("SnSSubskill_ID");
        t.HasOne<InterventionPackage>().WithMany().HasForeignKey(x => x.InterventionPackageId).OnDelete(DeleteBehavior.NoAction);
        t.HasOne<InterventionTheory>().WithMany().HasForeignKey(x => x.InterventionTheoryId).OnDelete(DeleteBehavior.NoAction);
        t.HasOne<Subskill>().WithMany().HasForeignKey(x => x.SnSsubskillId).OnDelete(DeleteBehavior.NoAction);
        t.HasIndex(x => new { x.InterventionPackageId, x.InterventionTheoryId }).IsUnique();
        t.HasIndex(x => new { x.InterventionPackageId, x.SortOrder }).IsUnique();
        var q = modelBuilder.Entity<InterventionPackageQuestion>();
        q.ToTable("InterventionPackageQuestion", "public"); q.HasKey(x => x.InterventionPackageQuestionId);
        q.Property(x => x.InterventionPackageQuestionId).HasColumnName("InterventionPackageQuestion_ID").UseIdentityAlwaysColumn();
        q.Property(x => x.InterventionPackageId).HasColumnName("InterventionPackage_ID");
        q.Property(x => x.QuestionId).HasColumnName("Question_ID");
        q.Property(x => x.SnSquestionText).HasColumnName("SnSQuestionText");
        q.Property(x => x.SnSoptionA).HasColumnName("SnSOptionA"); q.Property(x => x.SnSoptionB).HasColumnName("SnSOptionB");
        q.Property(x => x.SnSoptionC).HasColumnName("SnSOptionC"); q.Property(x => x.SnSoptionD).HasColumnName("SnSOptionD");
        q.Property(x => x.SnSanswerKey).HasColumnName("SnSAnswerKey"); q.Property(x => x.SnSexplanation).HasColumnName("SnSExplanation");
        q.Property(x => x.SnSsubskillId).HasColumnName("SnSSubskill_ID");
        q.HasOne<InterventionPackage>().WithMany().HasForeignKey(x => x.InterventionPackageId).OnDelete(DeleteBehavior.NoAction);
        q.HasOne<Question>().WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.NoAction);
        q.HasOne<Subskill>().WithMany().HasForeignKey(x => x.SnSsubskillId).OnDelete(DeleteBehavior.NoAction);
        q.HasIndex(x => new { x.InterventionPackageId, x.QuestionId }).IsUnique();
        q.HasIndex(x => new { x.InterventionPackageId, x.QuestionNumber }).IsUnique();
    }
}
