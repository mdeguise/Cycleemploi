namespace TremblantLifecycle.Api.Models.Dtos;

public class D365ApproverDto
{
    public int D365ApproverId { get; set; }
    public string Sam { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string? Email { get; set; }

    /// <summary>Null = global approver (any request).</summary>
    public string? PositionTitle { get; set; }

    public DateTime CreatedAt { get; set; }
    public string? CreatedByDisplayName { get; set; }
}

public class CreateD365ApproverDto
{
    public string Sam { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string? Email { get; set; }
    public string? PositionTitle { get; set; }
}

/// <summary>"IT Personnel" — sees the tracking list and every request's status, never the Envoyer
/// action. See D365Viewer's doc comment.</summary>
public class D365ViewerDto
{
    public int D365ViewerId { get; set; }
    public string Sam { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string? Email { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedByDisplayName { get; set; }
}

public class CreateD365ViewerDto
{
    public string Sam { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string? Email { get; set; }
}

/// <summary>One peer employee (same Job Code and Position Title) and the D365 security roles they
/// already hold, per the imported D365UserSecurityRole snapshot — shown to an approver as reference
/// context so they aren't picking roles blind.</summary>
public class D365PeerRoleDto
{
    public string EmployeeName { get; set; } = null!;
    public string EmployeeId { get; set; } = null!;
    public List<string> Roles { get; set; } = [];
}

/// <summary>One row in the D365 approvals tracking screen (Administration > Approbations D365).</summary>
public class D365AccessApprovalSummaryDto
{
    public int RequestId { get; set; }
    public string RequestNumber { get; set; } = null!;
    public string EmployeeName { get; set; } = null!;
    public string? PositionTitle { get; set; }
    public string? ManagerName { get; set; }
    public string RequesterName { get; set; } = null!;
    public DateOnly? StartDate { get; set; }

    /// <summary>"Pending" or "Completed" — see D365ApprovalStatus. Completed doesn't imply the TDX
    /// ticket itself succeeded; TicketNumber/TicketState reflect that separately.</summary>
    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletedByDisplayName { get; set; }

    public string? TicketNumber { get; set; }
    public string? TicketState { get; set; }
    public string? TicketStateLabel { get; set; }
}

/// <summary>The prepopulated French approval form — everything the app already knows, everything
/// still needed from the approver, and the peer-comparison list.</summary>
public class D365AccessApprovalDetailDto
{
    public int RequestId { get; set; }
    public string RequestNumber { get; set; } = null!;
    public string Status { get; set; } = null!;

    /// <summary>False for a viewer who can see this (an Administration Admin, for oversight) but is
    /// not a matched D365Approver — the Envoyer action stays gated to whoever the email actually
    /// went to.</summary>
    public bool CanComplete { get; set; }

    // ---- Prepopulated, read-only ----
    public string RequesterName { get; set; } = null!;
    public string EmployeeName { get; set; } = null!;
    public string? EmployeeEmail { get; set; }
    public string? ManagerName { get; set; }
    public string? PositionTitle { get; set; }
    public string? JobCode { get; set; }
    public string? Departement { get; set; }
    public DateOnly? StartDate { get; set; }

    /// <summary>Fixed at "6201" for every request — shown for transparency, never an approver
    /// input. See D365AccessApprovalsController.FixedLegalEntity.</summary>
    public string LegalEntity { get; set; } = null!;

    /// <summary>The employee's Workday Cost_Center, verbatim — shown for transparency, never an
    /// approver input.</summary>
    public string? DepartmentNumber { get; set; }

    /// <summary>"New Access" | "Change Access" | "Remove Access" — see
    /// D365AccessApproval.AccessType's doc comment. Defaults to "New Access" for approvals from
    /// the wizard-driven onboarding flow, which never asks.</summary>
    public string AccessType { get; set; } = null!;

    // ---- Editable by the approver (prefilled if already Completed) ----
    public string? JobTitleEnglish { get; set; }
    public decimal? ApprovalLimit { get; set; }
    public string? ApAccessDetails { get; set; }
    public string? AdditionalLegalEntities { get; set; }
    public string? DefaultShippingAddress { get; set; }
    public string? Comments { get; set; }
    public bool? LevyEmployee { get; set; }
    public List<string> Roles { get; set; } = [];

    /// <summary>The fixed 10-item TDX role-checkbox catalog — see TdxD365RoleCheckboxes.All.</summary>
    public List<string> RoleCatalog { get; set; } = [];

    public List<D365PeerRoleDto> Peers { get; set; } = [];
}

public class CompleteD365AccessApprovalDto
{
    public string JobTitleEnglish { get; set; } = null!;
    public decimal ApprovalLimit { get; set; }
    public bool LevyEmployee { get; set; }
    public string? ApAccessDetails { get; set; }
    public string? AdditionalLegalEntities { get; set; }
    public string? DefaultShippingAddress { get; set; }
    public string? Comments { get; set; }
    public List<string> Roles { get; set; } = [];
}

public class CompleteD365AccessApprovalResultDto
{
    public bool Succeeded { get; set; }
    public string? TicketNumber { get; set; }
    public string? Error { get; set; }
}

/// <summary>Everything the standalone D365AccessRequest app needs to prefill its form once a D365
/// Approver has picked an employee from Workday — same shape as D365AccessApprovalDetailDto's
/// read-only section, but derived directly from Workday rather than from an existing approval,
/// since none exists yet at this point.</summary>
public class D365AdHocPrefillDto
{
    public string WorkdayEmployeeId { get; set; } = null!;
    public string EmployeeName { get; set; } = null!;
    public string? EmployeeEmail { get; set; }
    public string? ManagerName { get; set; }
    public string? PositionTitle { get; set; }
    public string? JobCode { get; set; }
    public string? Departement { get; set; }

    /// <summary>Fixed at "6201" for every request — shown for transparency, never an editable input.</summary>
    public string LegalEntity { get; set; } = null!;

    /// <summary>The employee's Workday Cost_Center, verbatim — shown for transparency, never an
    /// editable input.</summary>
    public string? DepartmentNumber { get; set; }

    /// <summary>"{Job_Profile} - {Position_Title}" starting point for Job Title (English) — see
    /// D365AccessApprovalsController.BuildDefaultJobTitle. The requester can edit it.</summary>
    public string? JobTitleEnglishSuggestion { get; set; }

    public List<string> RoleCatalog { get; set; } = [];
    public List<D365PeerRoleDto> Peers { get; set; } = [];

    /// <summary>"New Access" | "Change Access" | "Remove Access" — see
    /// D365AccessApprovalsController.AllowedAccessTypes.</summary>
    public List<string> AccessTypeCatalog { get; set; } = [];
}

/// <summary>Submits a brand-new, fully-filled-out D365 access request for an employee who was NOT
/// going through the onboarding/réactivation wizard — e.g. access needed after the fact. Creates a
/// Pending D365AccessApproval exactly like the wizard-driven path does, so it still goes through
/// the same matched-approver review before anything reaches TDX; the difference is only WHERE the
/// fields came from (the requester filled them in directly, not an approver reviewing a bare
/// request).</summary>
public class SubmitAdHocD365AccessDto
{
    public string WorkdayEmployeeId { get; set; } = null!;

    /// <summary>Must be one of D365AccessApprovalsController.AllowedAccessTypes.</summary>
    public string AccessType { get; set; } = null!;

    public string JobTitleEnglish { get; set; } = null!;
    public decimal ApprovalLimit { get; set; }
    public bool LevyEmployee { get; set; }
    public string? ApAccessDetails { get; set; }
    public string? AdditionalLegalEntities { get; set; }
    public string? DefaultShippingAddress { get; set; }
    public string? Comments { get; set; }
    public List<string> Roles { get; set; } = [];
}

public class SubmitAdHocD365AccessResultDto
{
    public int RequestId { get; set; }
    public string RequestNumber { get; set; } = null!;
}
