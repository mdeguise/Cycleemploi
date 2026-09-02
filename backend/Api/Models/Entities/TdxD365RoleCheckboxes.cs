namespace TremblantLifecycle.Api.Models.Entities;

/// <summary>The fixed set of role checkboxes on TDX's "D365 - Access" form (FormID 10799) — re-read
/// directly from a real export of that form 2026-08-09 (D365 - Access.pdf). Kept as backend
/// constants, same pattern as other small/static catalogs in this app. Used both by
/// D365AccessApproval's Roles and by TdxService's RoleShortLabels mapping.</summary>
public static class TdxD365RoleCheckboxes
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
