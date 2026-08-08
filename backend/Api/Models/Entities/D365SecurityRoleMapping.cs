namespace TremblantLifecycle.Api.Models.Entities;

/// <summary>Maps a Workday job code to one or more D365 security roles — a job code can need
/// several roles (e.g. an accountant needing both AP and GL access). Used when a request selects
/// "Accès D365" to resolve which role checkboxes to set on the TDX "D365 - Access" ticket (FormID
/// 10799), by looking up the employee's job code(s). Managed via a small admin page in the app —
/// this table starts empty and is populated over time, there's no external source to sync from.</summary>
public class D365SecurityRoleMapping
{
    public int Id { get; set; }
    public string JobCode { get; set; } = null!;
    public string Role { get; set; } = null!;
}

/// <summary>The fixed set of D365 security roles a job code can be mapped to — matches the checkbox
/// options on TDX's "D365 - Access" form (FormID 10799) exactly, confirmed against a real export of
/// that form. Kept as backend constants rather than a lookup table, same pattern as the other
/// small/static catalogs in this app (see AccessDetail's doc comment).</summary>
public static class D365SecurityRoles
{
    public const string ProcurementApproverRequester = "Procurement - Approver/Requester";
    public const string ProcurementProjectManager = "Procurement - Project Manager";
    public const string ProcurementReceiver = "Procurement - Receiver";
    public const string AccountsPayableAccess = "Accounts Payable - Access (must be accountant)";
    public const string GeneralLedgerJePreparer = "General Ledger - JE Preparer / Accountant";
    public const string GeneralLedgerJeReviewer = "General Ledger - JE Reviewer / Sr. Accountant";
    public const string FinancialReportingResortSpecific = "Financial Reporting - Resort Specific";
    public const string FinancialReportingDenverCorp = "Financial Reporting - Denver/Corp";
    public const string AccountsReceivableClerk = "Accounts Receivable - Clerk";
    public const string AccountsReceivableManager = "Accounts Receivable - Manager";

    public static readonly IReadOnlyList<string> All =
    [
        ProcurementApproverRequester,
        ProcurementProjectManager,
        ProcurementReceiver,
        AccountsPayableAccess,
        GeneralLedgerJePreparer,
        GeneralLedgerJeReviewer,
        FinancialReportingResortSpecific,
        FinancialReportingDenverCorp,
        AccountsReceivableClerk,
        AccountsReceivableManager,
    ];
}
