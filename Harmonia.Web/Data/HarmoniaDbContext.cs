using System;
using System.Collections.Generic;
using Harmonia.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Harmonia.Web.Data;

public partial class HarmoniaDbContext : DbContext
{
    public HarmoniaDbContext(DbContextOptions<HarmoniaDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Assessment> Assessments { get; set; }

    public virtual DbSet<Attempt> Attempts { get; set; }

    public virtual DbSet<Class> Classes { get; set; }

    public virtual DbSet<ClassMembership> ClassMemberships { get; set; }

    public virtual DbSet<CurriculumUnit> CurriculumUnits { get; set; }

    public virtual DbSet<ImportBatch> ImportBatches { get; set; }

    public virtual DbSet<InterventionPackage> InterventionPackages { get; set; }

    public virtual DbSet<InterventionPackageFollowup> InterventionPackageFollowups { get; set; }

    public virtual DbSet<InterventionPackageResource> InterventionPackageResources { get; set; }

    public virtual DbSet<InterventionPackageStudent> InterventionPackageStudents { get; set; }

    public virtual DbSet<InterventionResource> InterventionResources { get; set; }

    public virtual DbSet<KnowledgeArea> KnowledgeAreas { get; set; }

    public virtual DbSet<Question> Questions { get; set; }

    public virtual DbSet<QuestionInTest> QuestionInTests { get; set; }

    public virtual DbSet<Response> Responses { get; set; }

    public virtual DbSet<Student> Students { get; set; }

    public virtual DbSet<Subskill> Subskills { get; set; }

    public virtual DbSet<Teacher> Teachers { get; set; }

    public virtual DbSet<TeacherClassAssignment> TeacherClassAssignments { get; set; }

    public virtual DbSet<Test> Tests { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("btree_gist");

        modelBuilder.Entity<Assessment>(entity =>
        {
            entity.HasKey(e => e.AssessmentId).HasName("Assessment_pkey");

            entity.ToTable("Assessment");

            entity.Property(e => e.AssessmentId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("Assessment_ID");
            entity.Property(e => e.AssessmentName).HasMaxLength(255);
            entity.Property(e => e.ClassId).HasColumnName("Class_ID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CreatedByUserId).HasColumnName("CreatedByUser_ID");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'Draft'::character varying");
            entity.Property(e => e.TeacherId).HasColumnName("Teacher_ID");
            entity.Property(e => e.TestId).HasColumnName("Test_ID");
            entity.Property(e => e.UpdatedByUserId).HasColumnName("UpdatedByUser_ID");

            entity.HasOne(d => d.Class).WithMany(p => p.Assessments)
                .HasForeignKey(d => d.ClassId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Assessment_Class_ID_fkey");

            entity.HasOne(d => d.Teacher).WithMany(p => p.Assessments)
                .HasForeignKey(d => d.TeacherId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Assessment_Teacher_ID_fkey");

            entity.HasOne(d => d.Test).WithMany(p => p.Assessments)
                .HasForeignKey(d => d.TestId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Assessment_Test_ID_fkey");
        });

        modelBuilder.Entity<Attempt>(entity =>
        {
            entity.HasKey(e => e.AttemptId).HasName("Attempt_pkey");

            entity.ToTable("Attempt");

            entity.HasIndex(e => new { e.StudentId, e.AssessmentId }, "CU_StudentAssessment").IsUnique();

            entity.Property(e => e.AttemptId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("Attempt_ID");
            entity.Property(e => e.AssessmentId).HasColumnName("Assessment_ID");
            entity.Property(e => e.ImportBatchId).HasColumnName("ImportBatch_ID");
            entity.Property(e => e.Score).HasPrecision(5, 2);
            entity.Property(e => e.StudentId).HasColumnName("Student_ID");

            entity.HasOne(d => d.Assessment).WithMany(p => p.Attempts)
                .HasForeignKey(d => d.AssessmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Attempt_Assessment_ID_fkey");

            entity.HasOne(d => d.ImportBatch).WithMany(p => p.Attempts)
                .HasForeignKey(d => d.ImportBatchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Attempt_ImportBatch_ID_fkey");

            entity.HasOne(d => d.Student).WithMany(p => p.Attempts)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Attempt_Student_ID_fkey");
        });

        modelBuilder.Entity<Class>(entity =>
        {
            entity.HasKey(e => e.ClassId).HasName("Class_pkey");

            entity.ToTable("Class");

            entity.HasIndex(e => new { e.ClassCode, e.AcademicYear }, "CU_ClassCode_AcademicYear").IsUnique();

            entity.Property(e => e.ClassId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("Class_ID");
            entity.Property(e => e.AcademicYear).HasMaxLength(9);
            entity.Property(e => e.ClassCode).HasMaxLength(50);
            entity.Property(e => e.ClassName).HasMaxLength(150);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CreatedByUserId).HasColumnName("CreatedByUser_ID");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'Active'::character varying");
            entity.Property(e => e.UpdatedByUserId).HasColumnName("UpdatedByUser_ID");
        });

        modelBuilder.Entity<ClassMembership>(entity =>
        {
            entity.HasKey(e => e.ClassMembershipId).HasName("ClassMembership_pkey");

            entity.ToTable("ClassMembership");

            entity.Property(e => e.ClassMembershipId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("ClassMembership_ID");
            entity.Property(e => e.ClassId).HasColumnName("Class_ID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CreatedByUserId).HasColumnName("CreatedByUser_ID");
            entity.Property(e => e.StudentId).HasColumnName("Student_ID");
            entity.Property(e => e.UpdatedByUserId).HasColumnName("UpdatedByUser_ID");

            entity.HasOne(d => d.Class).WithMany(p => p.ClassMemberships)
                .HasForeignKey(d => d.ClassId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ClassMembership_Class_ID_fkey");

            entity.HasOne(d => d.Student).WithMany(p => p.ClassMemberships)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ClassMembership_Student_ID_fkey");
        });

        modelBuilder.Entity<CurriculumUnit>(entity =>
        {
            entity.HasKey(e => e.CurriculumUnitId).HasName("CurriculumUnit_pkey");

            entity.ToTable("CurriculumUnit");

            entity.HasIndex(e => new { e.Grade, e.UnitNumber }, "CK_UnitNumberGrade").IsUnique();

            entity.Property(e => e.CurriculumUnitId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("CurriculumUnit_ID");
        });

        modelBuilder.Entity<ImportBatch>(entity =>
        {
            entity.HasKey(e => e.ImportBatchId).HasName("ImportBatch_pkey");

            entity.ToTable("ImportBatch");

            entity.Property(e => e.ImportBatchId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("ImportBatch_ID");
            entity.Property(e => e.AssessmentId).HasColumnName("Assessment_ID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.ImportedByUserId).HasColumnName("ImportedByUser_ID");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'Pending'::character varying");

            entity.HasOne(d => d.Assessment).WithMany(p => p.ImportBatches)
                .HasForeignKey(d => d.AssessmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ImportBatch_Assessment_ID_fkey");
        });

        modelBuilder.Entity<InterventionPackage>(entity =>
        {
            entity.HasKey(e => e.InterventionPackageId).HasName("InterventionPackage_pkey");

            entity.ToTable("InterventionPackage");

            entity.Property(e => e.InterventionPackageId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("InterventionPackage_ID");
            entity.Property(e => e.AssessmentId).HasColumnName("Assessment_ID");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'Draft'::character varying");

            entity.HasOne(d => d.Assessment).WithMany(p => p.InterventionPackages)
                .HasForeignKey(d => d.AssessmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("InterventionPackage_Assessment_ID_fkey");
        });

        modelBuilder.Entity<InterventionPackageFollowup>(entity =>
        {
            entity.HasKey(e => e.InterventionPackageFollowupId).HasName("InterventionPackageFollowup_pkey");

            entity.ToTable("InterventionPackageFollowup");

            entity.HasIndex(e => new { e.InterventionPackageId, e.FollowupAssessmentId }, "CU_PackageFollowupAssessment").IsUnique();

            entity.Property(e => e.InterventionPackageFollowupId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("InterventionPackageFollowup_ID");
            entity.Property(e => e.FollowupAssessmentId).HasColumnName("FollowupAssessment_ID");
            entity.Property(e => e.InterventionPackageId).HasColumnName("InterventionPackage_ID");

            entity.HasOne(d => d.FollowupAssessment).WithMany(p => p.InterventionPackageFollowups)
                .HasForeignKey(d => d.FollowupAssessmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("InterventionPackageFollowup_FollowupAssessment_ID_fkey");

            entity.HasOne(d => d.InterventionPackage).WithMany(p => p.InterventionPackageFollowups)
                .HasForeignKey(d => d.InterventionPackageId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("InterventionPackageFollowup_InterventionPackage_ID_fkey");
        });

        modelBuilder.Entity<InterventionPackageResource>(entity =>
        {
            entity.HasKey(e => e.InterventionPackageResourceId).HasName("InterventionPackageResource_pkey");

            entity.ToTable("InterventionPackageResource");

            entity.HasIndex(e => new { e.InterventionPackageId, e.InterventionResourceId }, "CU_PackageResource").IsUnique();

            entity.HasIndex(e => new { e.SortOrder, e.InterventionPackageId }, "CU_ResourceSortOrder").IsUnique();

            entity.Property(e => e.InterventionPackageResourceId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("InterventionPackageResource_ID");
            entity.Property(e => e.InterventionPackageId).HasColumnName("InterventionPackage_ID");
            entity.Property(e => e.InterventionResourceId).HasColumnName("InterventionResource_ID");

            entity.HasOne(d => d.InterventionPackage).WithMany(p => p.InterventionPackageResources)
                .HasForeignKey(d => d.InterventionPackageId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("InterventionPackageResource_InterventionPackage_ID_fkey");

            entity.HasOne(d => d.InterventionResource).WithMany(p => p.InterventionPackageResources)
                .HasForeignKey(d => d.InterventionResourceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("InterventionPackageResource_InterventionResource_ID_fkey");
        });

        modelBuilder.Entity<InterventionPackageStudent>(entity =>
        {
            entity.HasKey(e => e.InterventionPackageStudentId).HasName("InterventionPackageStudent_pkey");

            entity.ToTable("InterventionPackageStudent");

            entity.HasIndex(e => new { e.StudentId, e.InterventionPackageId }, "CU_PackageStudent").IsUnique();

            entity.Property(e => e.InterventionPackageStudentId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("InterventionPackageStudent_ID");
            entity.Property(e => e.InterventionPackageId).HasColumnName("InterventionPackage_ID");
            entity.Property(e => e.StudentId).HasColumnName("Student_ID");

            entity.HasOne(d => d.InterventionPackage).WithMany(p => p.InterventionPackageStudents)
                .HasForeignKey(d => d.InterventionPackageId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("InterventionPackageStudent_InterventionPackage_ID_fkey");

            entity.HasOne(d => d.Student).WithMany(p => p.InterventionPackageStudents)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("InterventionPackageStudent_Student_ID_fkey");
        });

        modelBuilder.Entity<InterventionResource>(entity =>
        {
            entity.HasKey(e => e.InterventionResourceId).HasName("InterventionResource_pkey");

            entity.ToTable("InterventionResource");

            entity.Property(e => e.InterventionResourceId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("InterventionResource_ID");
            entity.Property(e => e.ResourceUrl).HasColumnName("ResourceURL");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'Active'::character varying");
            entity.Property(e => e.SubskillId).HasColumnName("Subskill_ID");

            entity.HasOne(d => d.Subskill).WithMany(p => p.InterventionResources)
                .HasForeignKey(d => d.SubskillId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("InterventionResource_Subskill_ID_fkey");
        });

        modelBuilder.Entity<KnowledgeArea>(entity =>
        {
            entity.HasKey(e => e.KnowledgeAreaId).HasName("KnowledgeArea_pkey");

            entity.ToTable("KnowledgeArea");

            entity.HasIndex(e => e.KnowledgeAreaCode, "CU_KnowledgeAreaCode").IsUnique();

            entity.Property(e => e.KnowledgeAreaId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("KnowledgeArea_ID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CurriculumUnitId).HasColumnName("CurriculumUnit_ID");
            entity.Property(e => e.KnowledgeAreaCode).HasMaxLength(50);
            entity.Property(e => e.KnowledgeAreaName).HasMaxLength(255);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'Active'::character varying");

            entity.HasOne(d => d.CurriculumUnit).WithMany(p => p.KnowledgeAreas)
                .HasForeignKey(d => d.CurriculumUnitId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("KnowledgeArea_CurriculumUnit_ID_fkey");
        });

        modelBuilder.Entity<Question>(entity =>
        {
            entity.HasKey(e => e.QuestionId).HasName("Question_pkey");

            entity.ToTable("Question");

            entity.Property(e => e.UsagePurpose).IsRequired().HasMaxLength(20).HasDefaultValue("ForTest");

            entity.HasIndex(e => e.QuestionCode, "CU_QuestionCode").IsUnique();

            entity.Property(e => e.QuestionId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("Question_ID");
            entity.Property(e => e.AnswerKey).HasMaxLength(1);
            entity.Property(e => e.QuestionCode).HasMaxLength(50);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'Active'::character varying");
            entity.Property(e => e.SubskillId).HasColumnName("Subskill_ID");

            entity.HasOne(d => d.Subskill).WithMany(p => p.Questions)
                .HasForeignKey(d => d.SubskillId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Question_Subskill_ID_fkey");
        });

        modelBuilder.Entity<QuestionInTest>(entity =>
        {
            entity.HasKey(e => e.QuestionInTestId).HasName("QuestionInTest_pkey");

            entity.ToTable("QuestionInTest");

            entity.HasIndex(e => new { e.TestId, e.QuestionId }, "CU_TestQuestionInTest").IsUnique();

            entity.HasIndex(e => new { e.TestId, e.QuestionNumber }, "CU_TestQuestionNumber").IsUnique();

            entity.Property(e => e.QuestionInTestId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("QuestionInTest_ID");
            entity.Property(e => e.QuestionId).HasColumnName("Question_ID");
            entity.Property(e => e.SnSanswerKey)
                .HasMaxLength(1)
                .HasColumnName("SnSAnswerKey");
            entity.Property(e => e.SnSoptionA).HasColumnName("SnSOptionA");
            entity.Property(e => e.SnSoptionB).HasColumnName("SnSOptionB");
            entity.Property(e => e.SnSoptionC).HasColumnName("SnSOptionC");
            entity.Property(e => e.SnSoptionD).HasColumnName("SnSOptionD");
            entity.Property(e => e.SnSquestionText).HasColumnName("SnSQuestionText");
            entity.Property(e => e.SnSsubskillId).HasColumnName("SnSSubskill_ID");
            entity.Property(e => e.TestId).HasColumnName("Test_ID");

            entity.HasOne(d => d.Question).WithMany(p => p.QuestionInTests)
                .HasForeignKey(d => d.QuestionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("QuestionInTest_Question_ID_fkey");

            entity.HasOne(d => d.SnSsubskill).WithMany(p => p.QuestionInTests)
                .HasForeignKey(d => d.SnSsubskillId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("QuestionInTest_SnSSubskill_ID_fkey");

            entity.HasOne(d => d.Test).WithMany(p => p.QuestionInTests)
                .HasForeignKey(d => d.TestId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("QuestionInTest_Test_ID_fkey");
        });

        modelBuilder.Entity<Response>(entity =>
        {
            entity.HasKey(e => e.ResponseId).HasName("Response_pkey");

            entity.ToTable("Response");

            entity.HasIndex(e => new { e.AttemptId, e.QuestionInTestId }, "CU_AttemptQuestionInTest").IsUnique();

            entity.Property(e => e.ResponseId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("Response_ID");
            entity.Property(e => e.AttemptId).HasColumnName("Attempt_ID");
            entity.Property(e => e.QuestionInTestId).HasColumnName("QuestionInTest_ID");

            entity.HasOne(d => d.Attempt).WithMany(p => p.Responses)
                .HasForeignKey(d => d.AttemptId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Response_Attempt_ID_fkey");

            entity.HasOne(d => d.QuestionInTest).WithMany(p => p.Responses)
                .HasForeignKey(d => d.QuestionInTestId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Response_QuestionInTest_ID_fkey");
        });

        modelBuilder.Entity<Student>(entity =>
        {
            entity.HasKey(e => e.StudentId).HasName("PK_student");

            entity.ToTable("Student");

            entity.HasIndex(e => e.StudentCode, "CU_StudentCode").IsUnique();

            entity.Property(e => e.StudentId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("Student_ID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CreatedByUserId).HasColumnName("CreatedByUser_ID");
            entity.Property(e => e.FullName).HasMaxLength(150);
            entity.Property(e => e.Gender).HasMaxLength(20);
            entity.Property(e => e.PrimaryGuardianName).HasMaxLength(150);
            entity.Property(e => e.PrimaryGuardianPhonenumber).HasMaxLength(20);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'Active'::character varying");
            entity.Property(e => e.StudentCode).HasMaxLength(50);
            entity.Property(e => e.UpdatedByUserId).HasColumnName("UpdatedByUser_ID");
        });

        modelBuilder.Entity<Subskill>(entity =>
        {
            entity.HasKey(e => e.SubskillId).HasName("Subskill_pkey");

            entity.ToTable("Subskill");

            entity.HasIndex(e => e.SubskillCode, "CU_SubskillCode").IsUnique();

            entity.Property(e => e.SubskillId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("Subskill_ID");
            entity.Property(e => e.KnowledgeAreaId).HasColumnName("KnowledgeArea_ID");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'Active'::character varying");
            entity.Property(e => e.SubskillCode).HasMaxLength(50);
            entity.Property(e => e.SubskillName).HasMaxLength(255);

            entity.HasOne(d => d.KnowledgeArea).WithMany(p => p.Subskills)
                .HasForeignKey(d => d.KnowledgeAreaId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Subskill_KnowledgeArea_ID_fkey");
        });

        modelBuilder.Entity<Teacher>(entity =>
        {
            entity.HasKey(e => e.TeacherId).HasName("Teacher_pkey");

            entity.ToTable("Teacher");

            entity.HasIndex(e => e.Email, "CU_Email").IsUnique();

            entity.HasIndex(e => e.PhoneNumber, "CU_PhoneNumber").IsUnique();

            entity.HasIndex(e => e.TeacherCode, "CU_TeacherCode").IsUnique();

            entity.Property(e => e.TeacherId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("Teacher_ID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CreatedByUserId).HasColumnName("CreatedByUser_ID");
            entity.Property(e => e.Email).HasMaxLength(150);
            entity.Property(e => e.FullName).HasMaxLength(150);
            entity.Property(e => e.PhoneNumber).HasMaxLength(20);
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.Property(e => e.TeacherCode).HasMaxLength(50);
            entity.Property(e => e.UpdatedByUserId).HasColumnName("UpdatedByUser_ID");
        });

        modelBuilder.Entity<TeacherClassAssignment>(entity =>
        {
            entity.HasKey(e => e.TeacherClassAssignmentId).HasName("TeacherClassAssignment_pkey");

            entity.ToTable("TeacherClassAssignment");

            entity.Property(e => e.TeacherClassAssignmentId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("TeacherClassAssignment_ID");
            entity.Property(e => e.ClassId).HasColumnName("Class_ID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CreatedByUserId).HasColumnName("CreatedByUser_ID");
            entity.Property(e => e.TeacherId).HasColumnName("Teacher_ID");
            entity.Property(e => e.UpdatedByUserId).HasColumnName("UpdatedByUser_ID");

            entity.HasOne(d => d.Class).WithMany(p => p.TeacherClassAssignments)
                .HasForeignKey(d => d.ClassId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("TeacherClassAssignment_Class_ID_fkey");

            entity.HasOne(d => d.Teacher).WithMany(p => p.TeacherClassAssignments)
                .HasForeignKey(d => d.TeacherId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("TeacherClassAssignment_Teacher_ID_fkey");
        });

        modelBuilder.Entity<Test>(entity =>
        {
            entity.HasKey(e => e.TestId).HasName("Test_pkey");

            entity.ToTable("Test");

            entity.Property(e => e.TestId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("Test_ID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CreatedByUserId).HasColumnName("CreatedByUser_ID");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'Draft'::character varying");
            entity.Property(e => e.TestName).HasMaxLength(255);
            entity.Property(e => e.UpdatedByUserId).HasColumnName("UpdatedByUser_ID");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
