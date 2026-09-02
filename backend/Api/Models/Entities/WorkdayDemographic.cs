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

    /// <summary>Workday's own job profile label — unlike PositionTitle (French-only at Tremblant),
    /// this is already in English (e.g. "0115U - Maintenance Attendant"), confirmed against real
    /// data. Used to build the default "Titre du poste (anglais)" on the D365 access approval form.</summary>
    public string? JobProfile { get; set; }
    public string? JobFamilyGroup { get; set; }
    public string? TimeType { get; set; }
    public string? WorkerType { get; set; }
    public string? Manager { get; set; }
    public string? ManagerId { get; set; }
    public string? PayGroup { get; set; }
    public DateTime? HireDate { get; set; }

    /// <summary>Workday's internal position code (e.g. "P-01059305-TR") — distinct from
    /// PositionTitle (the human-readable job title).</summary>
    public string? Position { get; set; }

    public string? CostCenter { get; set; }
    public DateTime? SeniorityDate { get; set; }

    /// <summary>True/false, distinct from EmploymentStatus (which is the "Active"/"Inactive"/
    /// "Terminated" text field) — confirmed against real data as a bit column.</summary>
    public bool? ActiveStatus { get; set; }

    public string? LeaveType { get; set; }
    public string? EstimatedLastDayOfLeave { get; set; }

    /// <summary>Real values confirmed against live data: "Active", "Inactive", "Terminated".
    /// "Inactive" covers on-leave/layoff, which the business treats as active for this app's
    /// purposes (see the "seuls les employés actifs" notice in the wizard) — filter with
    /// != "Terminated", not == "Active".</summary>
    public string? EmploymentStatus { get; set; }

    public string? TerminationReason { get; set; }
    public DateTime? TerminationDate { get; set; }
    public string? WorkEmail { get; set; }
    public string? Email { get; set; }

    /// <summary>True when this row is the employee's primary job assignment. Employee search/selection
    /// must filter to PrimaryJob == true (or otherwise collapse to one row per Employee_ID) — this table
    /// has multiple rows per employee when someone holds concurrent positions.
    /// Confirmed against real data as a bit column (like ActiveStatus), NOT an int: modelling it as
    /// int? still filtered correctly, because EF translates the comparison to SQL server-side, but
    /// threw InvalidCastException (Boolean -> Int32) the moment the full entity was materialized.</summary>
    public bool? PrimaryJob { get; set; }
}
