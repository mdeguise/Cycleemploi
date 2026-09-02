using Microsoft.EntityFrameworkCore;
using TremblantLifecycle.Api.Models.Entities;

namespace TremblantLifecycle.Api.Data;

/// <summary>Read-only context pointed at the existing Redingote database — separate from
/// AppDbContext (the new EmployeeLifecycle database) because these are two genuinely different,
/// separately-owned databases on the same vm-trm-sql1 instance. Never call SaveChanges on this
/// context; WorkdayDemographic is externally managed (hourly Workday sync) and this app must never
/// write to it.</summary>
public class WorkdayContext : DbContext
{
    public WorkdayContext(DbContextOptions<WorkdayContext> options) : base(options) { }

    public DbSet<WorkdayDemographic> WorkdayDemographics => Set<WorkdayDemographic>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkdayDemographic>(entity =>
        {
            entity.ToTable("WorkdayDemographic", schema: "dbo");
            entity.HasNoKey();
            entity.Property(e => e.EmployeeId).HasColumnName("Employee_ID");
            entity.Property(e => e.FirstName).HasColumnName("First_Name");
            entity.Property(e => e.PreferredFirstName).HasColumnName("Preferred_First_Name");
            entity.Property(e => e.LastName).HasColumnName("Last_Name");
            entity.Property(e => e.PositionTitle).HasColumnName("Position_Title");
            entity.Property(e => e.JobCode).HasColumnName("Job_Code");
            entity.Property(e => e.JobProfile).HasColumnName("Job_Profile");
            entity.Property(e => e.JobFamilyGroup).HasColumnName("Job_Family_Group");
            entity.Property(e => e.TimeType).HasColumnName("Time_Type");
            entity.Property(e => e.WorkerType).HasColumnName("Worker_Type");
            entity.Property(e => e.Manager).HasColumnName("Manager");
            entity.Property(e => e.ManagerId).HasColumnName("Manager_ID");
            entity.Property(e => e.PayGroup).HasColumnName("Pay_Group");
            entity.Property(e => e.HireDate).HasColumnName("Hire_Date");
            entity.Property(e => e.Position).HasColumnName("Position");
            entity.Property(e => e.CostCenter).HasColumnName("Cost_Center");
            entity.Property(e => e.SeniorityDate).HasColumnName("Seniority_Date");
            entity.Property(e => e.ActiveStatus).HasColumnName("Active_Status");
            entity.Property(e => e.LeaveType).HasColumnName("Leave_Type");
            entity.Property(e => e.EstimatedLastDayOfLeave).HasColumnName("Estimated_Last_Day_of_Leave");
            entity.Property(e => e.EmploymentStatus).HasColumnName("Employment_Status");
            entity.Property(e => e.TerminationReason).HasColumnName("Termination_Reason");
            entity.Property(e => e.TerminationDate).HasColumnName("Termination_Date");
            entity.Property(e => e.WorkEmail).HasColumnName("Work_Email");
            entity.Property(e => e.Email).HasColumnName("Email");
            entity.Property(e => e.PrimaryJob).HasColumnName("Primary_Job");
        });
    }
}
