namespace TremblantLifecycle.Api.Models.Entities;

/// <summary>The fixed routing roles that replaced Workday-Position_Title-based approver matching —
/// see D365Approver.ApprovalRole's doc comment. A request needing Dynaway routes to Dynaway; every
/// other D365 request routes through Stage1 then Stage2, in order.</summary>
public static class D365ApprovalRoles
{
    /// <summary>Sole approver for any request where "Besoin de gestion des actifs (Asset
    /// Management) avec Dynaway" was checked — single-stage, same as Stage1+Stage2 combined into
    /// one step for this specific case.</summary>
    public const string Dynaway = "Dynaway";

    /// <summary>First approver for every non-Dynaway request — reviews/fills the form (roles,
    /// access type, approval limit, etc.) and either completes it (advancing to Stage2) or rejects
    /// it outright.</summary>
    public const string Stage1 = "Stage1";

    /// <summary>Second and final approver for every non-Dynaway request — reviews what Stage1
    /// filled in (read-only) and either confirms it (creating the TDX ticket) or rejects it.</summary>
    public const string Stage2 = "Stage2";

    public static readonly string[] All = [Dynaway, Stage1, Stage2];

    public static bool IsValid(string? role) => role is not null && All.Contains(role);
}
