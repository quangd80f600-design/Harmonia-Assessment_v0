using Harmonia.Web.Models.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace Harmonia.Web.Data;
public sealed class HarmoniaIdentityDbContext(DbContextOptions<HarmoniaIdentityDbContext> options) : IdentityDbContext<HarmoniaUser>(options)
{
    protected override Version SchemaVersion => Microsoft.AspNetCore.Identity.IdentitySchemaVersions.Version2;
    public DbSet<TeacherUserLink> TeacherUserLinks => Set<TeacherUserLink>();
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("auth");
        builder.Entity<TeacherUserLink>(e => {
            e.ToTable("TeacherUserLinks"); e.HasKey(x => x.UserId);
            e.Property(x => x.UserId).HasColumnName("User_ID");
            e.Property(x => x.TeacherId).HasColumnName("Teacher_ID");
            e.HasIndex(x => x.TeacherId).IsUnique();
            e.HasOne(x => x.User).WithOne().HasForeignKey<TeacherUserLink>(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
// SQL generation must work without reading User Secrets or opening any database.
public sealed class IdentityDesignFactory : IDesignTimeDbContextFactory<HarmoniaIdentityDbContext>
{
    public HarmoniaIdentityDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<HarmoniaIdentityDbContext>()
        .UseNpgsql("Host=localhost;Database=harmonia_identity_design_only;Username=design_only", o => o.MigrationsHistoryTable("__EFMigrationsHistory", "auth")).Options);
}
