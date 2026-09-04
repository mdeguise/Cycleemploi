namespace TremblantLifecycle.Api.Models.Entities;

/// <summary>
/// Read-only mapping to the existing, externally-managed PROCESSES.dbo.vw_AdAccount_People view —
/// real people (IsPerson = 1) from the current-state AdAccount table, refreshed 3x/day by the
/// "AD - Refresh AdAccount" SQL Agent job (repo GroupMembershipSync) from a live ENTERPRISE.AD
/// query. Only ENTERPRISE.AD is loaded — deliberately, per that job's own notes: of the accounts
/// still left in iDirectory.itw, the near-totality already exist in ENTERPRISE.AD too, and the
/// rest are "_adm" admin accounts, not employees. Never written to by this app; only the columns
/// this app actually uses are mapped, the real view has far more.
/// </summary>
public class ProcessesAdAccount
{
    public string? SamAccountName { get; set; }
    public string? DisplayName { get; set; }
    public string? Mail { get; set; }
    public string? EmployeeID { get; set; }
    public bool? Enabled { get; set; }
}
