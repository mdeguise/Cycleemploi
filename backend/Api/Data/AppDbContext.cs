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
    public DbSet<OnboardingConfidentialComment> OnboardingConfidentialComments => Set<OnboardingConfidentialComment>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<D365SecurityRoleMapping> D365SecurityRoleMappings => Set<D365SecurityRoleMapping>();
    public DbSet<D365UserSecurityRole> D365UserSecurityRoles => Set<D365UserSecurityRole>();
    public DbSet<DynawayUser> DynawayUsers => Set<DynawayUser>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<TicketTemplate> TicketTemplates => Set<TicketTemplate>();
    public DbSet<RequestTicket> RequestTickets => Set<RequestTicket>();
    public DbSet<D365Approver> D365Approvers => Set<D365Approver>();
    public DbSet<D365Viewer> D365Viewers => Set<D365Viewer>();
    public DbSet<D365AccessApproval> D365AccessApprovals => Set<D365AccessApproval>();
    public DbSet<D365AccessApprovalRole> D365AccessApprovalRoles => Set<D365AccessApprovalRole>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Request>(entity =>
        {
            entity.HasIndex(r => r.RequestNumber).IsUnique();
            entity.Property(r => r.RequesterEmail).HasMaxLength(200);
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

        // Deliberately separate table/entity from OnboardingDetail — see the class doc comment on
        // OnboardingConfidentialComment for why this physical separation matters for access control.
        modelBuilder.Entity<OnboardingConfidentialComment>().HasKey(d => d.RequestId);
        modelBuilder.Entity<OnboardingConfidentialComment>()
            .HasOne(d => d.Request).WithOne(r => r.OnboardingConfidentialComment)
            .HasForeignKey<OnboardingConfidentialComment>(d => d.RequestId).OnDelete(DeleteBehavior.Cascade);

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

        modelBuilder.Entity<D365Approver>(entity =>
        {
            entity.Property(m => m.Sam).HasMaxLength(100).IsRequired();
            entity.Property(m => m.Email).HasMaxLength(200);
            entity.Property(m => m.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(m => m.PositionTitle).HasMaxLength(200);
            entity.Property(m => m.CreatedByDisplayName).HasMaxLength(200);
            // A person can be listed once as a global approver (PositionTitle null) and separately
            // scoped to specific titles — but not added twice for the exact same scope. HasFilter(null)
            // is essential, not cosmetic — see RequestTicket's doc comment for why: EF Core defaults a
            // unique index over a nullable column to "WHERE [PositionTitle] IS NOT NULL", which would
            // let the SAME person be added as a global approver (PositionTitle null) twice.
            entity.HasIndex(m => new { m.Sam, m.PositionTitle }).IsUnique().HasFilter(null);
        });

        modelBuilder.Entity<D365Viewer>(entity =>
        {
            entity.Property(m => m.Sam).HasMaxLength(100).IsRequired();
            entity.Property(m => m.Email).HasMaxLength(200);
            entity.Property(m => m.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(m => m.CreatedByDisplayName).HasMaxLength(200);
            entity.HasIndex(m => m.Sam).IsUnique();
        });

        modelBuilder.Entity<D365AccessApproval>(entity =>
        {
            entity.HasKey(d => d.RequestId);
            entity.HasOne(d => d.Request).WithOne(r => r.D365AccessApproval)
                .HasForeignKey<D365AccessApproval>(d => d.RequestId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(m => m.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(m => m.AccessType).HasMaxLength(20);
            entity.Property(m => m.JobTitleEnglish).HasMaxLength(200);
            entity.Property(m => m.LegalEntity).HasMaxLength(200);
            // Widened from a short numeric code — this is now always the employee's Workday
            // Cost_Center verbatim (e.g. "Lodging Maintenance (404006)"), not just a number.
            entity.Property(m => m.DepartmentNumber).HasMaxLength(200);
            entity.Property(m => m.ApprovalLimit).HasColumnType("decimal(18,2)");
            entity.Property(m => m.ApAccessDetails).HasMaxLength(2000);
            entity.Property(m => m.AdditionalLegalEntities).HasMaxLength(2000);
            entity.Property(m => m.DefaultShippingAddress).HasMaxLength(2000);
            entity.Property(m => m.Comments).HasMaxLength(2000);
            entity.Property(m => m.CompletedByObjectId).HasMaxLength(200);
            entity.Property(m => m.CompletedByDisplayName).HasMaxLength(200);
            entity.Property(m => m.CancelledByObjectId).HasMaxLength(200);
            entity.Property(m => m.CancelledByDisplayName).HasMaxLength(200);
            entity.Property(m => m.CancelReason).HasMaxLength(500);

            // NoAction, not Cascade: SQL Server refuses multiple cascade paths to the same table
            // (Request already cascades to RequestEmployee directly). Rows are cleaned up via the
            // Request cascade anyway.
            entity.HasOne<RequestEmployee>().WithMany()
                .HasForeignKey(d => d.RequestEmployeeId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<D365AccessApprovalRole>(entity =>
        {
            entity.Property(m => m.Role).HasMaxLength(200).IsRequired();
            entity.HasOne(m => m.D365AccessApproval).WithMany(a => a.Roles)
                .HasForeignKey(m => m.RequestId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(m => new { m.RequestId, m.Role }).IsUnique();
        });

        modelBuilder.Entity<RequestTicket>(entity =>
        {
            entity.Property(t => t.Kind).HasConversion<string>().HasMaxLength(40);
            entity.Property(t => t.Outcome).HasConversion<string>().HasMaxLength(20);
            entity.Property(t => t.TicketNumber).HasMaxLength(100);
            entity.Property(t => t.ErrorType).HasMaxLength(200);
            entity.Property(t => t.ErrorMessage).HasMaxLength(2000);

            entity.HasOne(t => t.Request)
                .WithMany()
                .HasForeignKey(t => t.RequestId)
                .OnDelete(DeleteBehavior.Cascade);

            // NoAction, not Cascade: SQL Server refuses multiple cascade paths to the same table, and
            // Request already cascades here. Rows are cleaned up via the Request cascade anyway.
            entity.HasOne(t => t.RequestEmployee)
                .WithMany()
                .HasForeignKey(t => t.RequestEmployeeId)
                .OnDelete(DeleteBehavior.NoAction);

            // One row per (request, kind, employee). This is what makes recording an outcome an
            // upsert and therefore makes retry idempotent — a retry can never create a second
            // ticket for the same slot.
            //
            // HasFilter(null) is essential, not cosmetic. EF Core defaults a unique index over a
            // NULLABLE column to "WHERE [RequestEmployeeId] IS NOT NULL", which would exclude every
            // request-level row (the Freshdesk parent and its two children all have a null
            // RequestEmployeeId) from the uniqueness guarantee — precisely the rows it is meant to
            // protect. Clearing the filter restores plain SQL Server semantics, where NULLs compare
            // equal in a unique index, so those kinds are genuinely limited to one row per request.
            entity.HasIndex(t => new { t.RequestId, t.Kind, t.RequestEmployeeId })
                .IsUnique()
                .HasFilter(null);
        });

        modelBuilder.Entity<AppUser>(entity =>
        {
            // Sam is the authorization key and therefore the unique one; Email is informational and
            // nullable, because admin (*_adm) accounts have no `mail` attribute in AD. See AppUser's
            // doc comment for why matching moved off Email.
            entity.Property(m => m.Sam).HasMaxLength(100).IsRequired();
            entity.Property(m => m.Email).HasMaxLength(200);
            entity.Property(m => m.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(m => m.CreatedByDisplayName).HasMaxLength(200);
            entity.Property(m => m.Role).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(m => m.Sam).IsUnique();
        });

        modelBuilder.Entity<TicketTemplate>(entity =>
        {
            entity.Property(m => m.Key).HasMaxLength(100).IsRequired();
            entity.Property(m => m.Content).IsRequired();
            entity.Property(m => m.UpdatedByDisplayName).HasMaxLength(200);
            entity.HasIndex(m => m.Key).IsUnique();
        });

        // Transactionally-safe request numbering — see RequestNumberService.
        modelBuilder.HasSequence<int>("RequestNumberSeq").StartsAt(1).IncrementsBy(1);
    }
}
