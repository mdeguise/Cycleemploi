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
    public DbSet<D365SecurityRoleMapping> D365SecurityRoleMappings => Set<D365SecurityRoleMapping>();
    public DbSet<D365UserSecurityRole> D365UserSecurityRoles => Set<D365UserSecurityRole>();
    public DbSet<DynawayUser> DynawayUsers => Set<DynawayUser>();
    public DbSet<D365JobCodeTemplate> D365JobCodeTemplates => Set<D365JobCodeTemplate>();
    public DbSet<D365JobCodeTemplateRole> D365JobCodeTemplateRoles => Set<D365JobCodeTemplateRole>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<TicketTemplate> TicketTemplates => Set<TicketTemplate>();

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

        modelBuilder.Entity<D365SecurityRoleMapping>(entity =>
        {
            entity.Property(m => m.JobCode).HasMaxLength(50).IsRequired();
            entity.Property(m => m.Role).HasMaxLength(200).IsRequired();
            entity.HasIndex(m => new { m.JobCode, m.Role }).IsUnique();
        });

        modelBuilder.Entity<D365UserSecurityRole>(entity =>
        {
            entity.Property(m => m.UserName).HasMaxLength(200).IsRequired();
            entity.Property(m => m.SecurityRole).HasMaxLength(200).IsRequired();
            entity.Property(m => m.EmployeeId).HasMaxLength(50);
            entity.Property(m => m.JobCode).HasMaxLength(50);
            entity.Property(m => m.PositionTitle).HasMaxLength(200);
        });

        modelBuilder.Entity<DynawayUser>(entity =>
        {
            entity.Property(m => m.Name).HasMaxLength(200);
            entity.Property(m => m.Login).HasMaxLength(100);
            entity.Property(m => m.PersonnelNumber).HasMaxLength(50);
            entity.HasIndex(m => m.Login);
        });

        modelBuilder.Entity<D365JobCodeTemplate>(entity =>
        {
            entity.Property(m => m.JobCode).HasMaxLength(50).IsRequired();
            entity.Property(m => m.JobTitleEnglish).HasMaxLength(200).IsRequired();
            entity.Property(m => m.LegalEntity).HasMaxLength(200).IsRequired();
            entity.Property(m => m.DepartmentNumber).HasMaxLength(50).IsRequired();
            entity.Property(m => m.ApprovalLimit).HasColumnType("decimal(18,2)");
            entity.Property(m => m.ApAccessDetails).HasMaxLength(2000);
            entity.Property(m => m.AdditionalLegalEntities).HasMaxLength(2000);
            entity.HasIndex(m => m.JobCode).IsUnique();
        });

        modelBuilder.Entity<D365JobCodeTemplateRole>(entity =>
        {
            entity.Property(m => m.Role).HasMaxLength(200).IsRequired();
            entity.HasOne(m => m.D365JobCodeTemplate).WithMany(t => t.Roles)
                .HasForeignKey(m => m.D365JobCodeTemplateId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(m => new { m.D365JobCodeTemplateId, m.Role }).IsUnique();
        });

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.Property(m => m.Email).HasMaxLength(200).IsRequired();
            entity.Property(m => m.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(m => m.CreatedByDisplayName).HasMaxLength(200);
            entity.HasIndex(m => m.Email).IsUnique();
        });

        modelBuilder.Entity<TicketTemplate>(entity =>
        {
            entity.Property(m => m.Key).HasMaxLength(100).IsRequired();
            entity.Property(m => m.Content).HasMaxLength(8000).IsRequired();
            entity.Property(m => m.UpdatedByDisplayName).HasMaxLength(200);
            entity.HasIndex(m => m.Key).IsUnique();
        });

        // Transactionally-safe request numbering — see RequestNumberService.
        modelBuilder.HasSequence<int>("RequestNumberSeq").StartsAt(1).IncrementsBy(1);
    }
}
