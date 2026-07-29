namespace TremblantLifecycle.Api.Models.Entities;

/// <summary>
/// Read-only mapping to the existing, externally-managed Redingote.dbo.WorkdayDemographic table,
/// synced hourly from Workday. Never written to by this app. One row per job assignment, not per
/// employee — an employee with multiple concurrent positions has multiple rows; PrimaryJob
/// distinguishes the primary one. Only the columns this app actually uses are mapped; the real
/// table has ~100 columns total.
/// </summary>
public class WorkdayDemographic
{
    public string EmployeeId { get; set; } = null!;
    public string? FirstName { get; set; }
    public string? PreferredFirstName { get; set; }
    public string? LastName { get; set; }
    public string? PositionTitle { get; set; }
    public string? JobCode { get; set; }
    public string? JobFamilyGroup { get; set; }
    public string? TimeType { get; set; }
    public string? WorkerType { get; set; }
    public string? Manager { get; set; }
    public string? ManagerId { get; set; }

    /// <summary>Real values confirmed against live data: "Active", "Inactive", "Terminated".
    /// "Inactive" covers on-leave/layoff, which the business treats as active for this app's
    /// purposes (see the "seuls les employés actifs" notice in the wizard) — filter with
    /// != "Terminated", not == "Active".</summary>
    public string? EmploymentStatus { get; set; }

    public string? TerminationReason { get; set; }
    public DateTime? TerminationDate { get; set; }
    public string? WorkEmail { get; set; }
    public string? Email { get; set; }

    /// <summary>1 when this row is the employee's primary job assignment. Employee search/selection
    /// must filter to PrimaryJob == 1 (or otherwise collapse to one row per Employee_ID) — this table
    /// has multiple rows per employee when someone holds concurrent positions.</summary>
    public int? PrimaryJob { get; set; }
}
