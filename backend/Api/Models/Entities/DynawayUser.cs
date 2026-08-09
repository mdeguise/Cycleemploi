namespace TremblantLifecycle.Api.Models.Entities;

/// <summary>One-time import of the D365/Dynaway "Dynaway License Details" list (from Power BI,
/// "tremblant_dynaway_users.csv"), used only by the reconciliation/discrepancy view. Login is the
/// AD sAMAccountName (the report's "User_" column) — the join key to Active Directory. The Dynaway
/// list is company-wide (all resorts); whether a given row is actually Tremblant is determined live
/// from AD (extensionAttribute2 = "T"), not stored here. Reference/audit data only; never edited by
/// the running app — refresh by re-importing (see the import script, not part of the app).</summary>
public class DynawayUser
{
    public int Id { get; set; }

    /// <summary>Display name as it appears in Dynaway (no resort suffix). May be blank on rows the
    /// source list had only an employee number for.</summary>
    public string? Name { get; set; }

    /// <summary>AD sAMAccountName (Dynaway "User_"). Blank on the few nameless source rows.</summary>
    public string? Login { get; set; }

    /// <summary>Dynaway PersonnelNumber (e.g. "EMP000017362"). Not the same as the AD/Workday
    /// EmployeeId, so it is not a reliable cross-system key — kept for reference only.</summary>
    public string? PersonnelNumber { get; set; }
}
