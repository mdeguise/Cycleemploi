namespace TremblantLifecycle.Api.Models.Entities;

/// <summary>One-time import of D365's current user->security-role assignments (from a TDX/D365
/// export, "Tremblant_D365_Security_Roles.xlsx"), enriched with each user's Workday EmployeeId,
/// primary job's JobCode, and PositionTitle (matched by name at import time — see the import
/// script, not part of the running app). Reference/audit data: this is what roles people actually
/// have today, as opposed to D365SecurityRoleMapping (JobCode -> role), which is the forward-looking
/// "what role should this job code get" table the admin page manages. EmployeeId/JobCode/
/// PositionTitle are null where the import script couldn't confidently match the user name to a
/// single Workday record — see the import's unmatched/ambiguous report.</summary>
public class D365UserSecurityRole
{
    public int Id { get; set; }
    public string UserName { get; set; } = null!;
    public string SecurityRole { get; set; } = null!;
    public string? EmployeeId { get; set; }
    public string? JobCode { get; set; }
    public string? PositionTitle { get; set; }
}
