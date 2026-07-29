using Microsoft.EntityFrameworkCore;
using TremblantLifecycle.Api.Models.Entities;

namespace TremblantLifecycle.Api.Data;

/// <summary>The new, app-owned database (proposed name: EmployeeLifecycle) — deliberately separate
/// from Redingote so a future change to the externally-managed Workday sync job can never touch
/// app data. See WorkdayContext for the read-only Redingote.dbo.WorkdayDemographic access.</summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Request> Requests => Set<Request>();
    public DbSet<RequestEmployee> RequestEmployees => Set<RequestEmployee>();
    public DbSet<OnboardingDetail> OnboardingDetails => Set<OnboardingDetail>();
    public DbSet<AccessDetail> AccessDetails => Set<AccessDetail>();
    public DbSet<RequestAccessSysteme> RequestAccessSystemes => Set<RequestAccessSysteme>();
    public DbSet<RequestAccessPos> RequestAccessPos => Set<RequestAccessPos>();
    public DbSet<EquipmentDetail> EquipmentDetails => Set<EquipmentDetail>();
    public DbSet<RequestEquipment> RequestEquipments => Set<RequestEquipment>();
    public DbSet<ApplicationsDetail> ApplicationsDetails => Set<ApplicationsDetail>();
    public DbSet<RequestApplication> RequestApplications => Set<RequestApplication>();
    public DbSet<OffboardingDetail> OffboardingDetails => Set<OffboardingDetail>();
    public DbSet<OffboardingConfidentialComment> OffboardingConfidentialComments => Set<OffboardingConfidentialComment>();
    public DbSet<Attachment> Attachments => Set<Attachment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Request>(entity =>
        {
            entity.HasIndex(r => r.RequestNumber).IsUnique();
            entity.Property(r => r.RequestType).HasConversion<string>().HasMaxLength(20);
            entity.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<RequestEmployee>()
            .HasOne(re => re.Request)
            .WithMany(r => r.Employees)
            .HasForeignKey(re => re.RequestId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<RequestEmployee>()
            .Property(re => re.WorkdayEmployeeId)
            .HasMaxLength(50);

        // 1:1 detail tables — RequestId is both PK and FK on each.
        modelBuilder.Entity<OnboardingDetail>().HasKey(d => d.RequestId);
        modelBuilder.Entity<OnboardingDetail>()
            .HasOne(d => d.Request).WithOne(r => r.OnboardingDetail)
            .HasForeignKey<OnboardingDetail>(d => d.RequestId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AccessDetail>().HasKey(d => d.RequestId);
        modelBuilder.Entity<AccessDetail>()
            .HasOne(d => d.Request).WithOne(r => r.AccessDetail)
            .HasForeignKey<AccessDetail>(d => d.RequestId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<RequestAccessSysteme>().HasKey(x => new { x.RequestId, x.Value });
        modelBuilder.Entity<RequestAccessSysteme>()
            .HasOne<AccessDetail>().WithMany(d => d.Systemes)
            .HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<RequestAccessPos>().HasKey(x => new { x.RequestId, x.Value });
        modelBuilder.Entity<RequestAccessPos>()
            .HasOne<AccessDetail>().WithMany(d => d.PosHebergement)
            .HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<EquipmentDetail>().HasKey(d => d.RequestId);
        modelBuilder.Entity<EquipmentDetail>()
            .HasOne(d => d.Request).WithOne(r => r.EquipmentDetail)
            .HasForeignKey<EquipmentDetail>(d => d.RequestId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<RequestEquipment>().HasKey(x => new { x.RequestId, x.Value });
        modelBuilder.Entity<RequestEquipment>()
            .HasOne<EquipmentDetail>().WithMany(d => d.Equipements)
            .HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ApplicationsDetail>().HasKey(d => d.RequestId);
        modelBuilder.Entity<ApplicationsDetail>()
            .HasOne(d => d.Request).WithOne(r => r.ApplicationsDetail)
            .HasForeignKey<ApplicationsDetail>(d => d.RequestId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<RequestApplication>().HasKey(x => new { x.RequestId, x.Value });
        modelBuilder.Entity<RequestApplication>()
            .HasOne<ApplicationsDetail>().WithMany(d => d.Applications)
            .HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<OffboardingDetail>().HasKey(d => d.RequestId);
        modelBuilder.Entity<OffboardingDetail>()
            .HasOne(d => d.Request).WithOne(r => r.OffboardingDetail)
            .HasForeignKey<OffboardingDetail>(d => d.RequestId).OnDelete(DeleteBehavior.Cascade);

        // Deliberately separate table/entity from OffboardingDetail — see the class doc comment on
        // OffboardingConfidentialComment for why this physical separation matters for access control.
        modelBuilder.Entity<OffboardingConfidentialComment>().HasKey(d => d.RequestId);
        modelBuilder.Entity<OffboardingConfidentialComment>()
            .HasOne(d => d.Request).WithOne(r => r.ConfidentialComment)
            .HasForeignKey<OffboardingConfidentialComment>(d => d.RequestId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Attachment>()
            .HasOne(a => a.Request).WithMany(r => r.Attachments)
            .HasForeignKey(a => a.RequestId).OnDelete(DeleteBehavior.Cascade);

        // Transactionally-safe request numbering — see RequestNumberService.
        modelBuilder.HasSequence<int>("RequestNumberSeq").StartsAt(1).IncrementsBy(1);
    }
}
