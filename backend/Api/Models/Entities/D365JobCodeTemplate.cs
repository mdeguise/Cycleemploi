namespace TremblantLifecycle.Api.Models.Entities;

/// <summary>One row per job code, capturing the answers to submit on the TDX "D365 - Access" form
/// (FormID 10799) when a new employee in that job code needs D365 access — the automation target
/// described 2026-08-09. Distinct from D365SecurityRoleMapping/D365UserSecurityRoles, which track
/// granular real D365 role names for reference/audit; this table's Roles use the TDX form's own
/// checkbox vocabulary (the 10 categories in TdxD365RoleCheckboxes.All) since that's literally what
/// the form accepts — nothing in D365 itself is called e.g. "Procurement - Approver/Requester".
/// Starts empty; a template's existence for a given job code is what "form filled out" means on the
/// admin list page.</summary>
public class D365JobCodeTemplate
{
    public int Id { get; set; }
    public string JobCode { get; set; } = null!;
    public string LegalEntity { get; set; } = null!;
    public string DepartmentNumber { get; set; } = null!;
    public decimal ApprovalLimit { get; set; }
    public string? ApAccessDetails { get; set; }
    public string? AdditionalLegalEntities { get; set; }

    /// <summary>Answers the form's "Levy Employee *" dropdown (Yes/No) — determined by job
    /// classification, so it's a per-job-code answer like everything else here.</summary>
    public bool LevyEmployee { get; set; }

    public List<D365JobCodeTemplateRole> Roles { get; set; } = [];
}

/// <summary>A single checked role box for a job code's template — many-to-many between
/// D365JobCodeTemplate and TdxD365RoleCheckboxes.All.</summary>
public class D365JobCodeTemplateRole
{
    public int Id { get; set; }
    public int D365JobCodeTemplateId { get; set; }
    public D365JobCodeTemplate D365JobCodeTemplate { get; set; } = null!;
    public string Role { get; set; } = null!;
}

/// <summary>The fixed set of role checkboxes on TDX's "D365 - Access" form (FormID 10799) — re-read
/// directly from a real export of that form 2026-08-09 (D365 - Access.pdf). Kept as backend
/// constants, same pattern as other small/static catalogs in this app.</summary>
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
