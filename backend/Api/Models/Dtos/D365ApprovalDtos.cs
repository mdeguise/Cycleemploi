namespace TremblantLifecycle.Api.Models.Dtos;

public class D365ApproverDto
{
    public int D365ApproverId { get; set; }
    public string Sam { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string? Email { get; set; }

    /// <summary>One of D365ApprovalRoles.All — see D365Approver.ApprovalRole.</summary>
    public string ApprovalRole { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
    public string? CreatedByDisplayName { get; set; }
}

public class CreateD365ApproverDto
{
    public string Sam { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string? Email { get; set; }
    public string ApprovalRole { get; set; } = null!;
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

    /// <summary>"Pending", "Stage1Approved", "Completed", "Cancelled" or "Rejected" — see
    /// D365ApprovalStatus. Completed doesn't imply the TDX ticket itself succeeded; TicketNumber/
    /// TicketState reflect that separately.</summary>
    public string Status { get; set; } = null!;

    /// <summary>True when this request needs Dynaway (single-stage, Dynaway-role approver only) —
    /// false means the normal Stage1-then-Stage2 chain applies.</summary>
    public bool IsDynawayPath { get; set; }

    public DateTime CreatedAt { get; set; }
    public string? Stage1ApprovedByDisplayName { get; set; }
    public DateTime? Stage1ApprovedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletedByDisplayName { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelledByDisplayName { get; set; }
    public string? CancelReason { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectedByDisplayName { get; set; }
    public string? RejectReason { get; set; }

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
    public string? CancelledByDisplayName { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }
    public string? Stage1ApprovedByDisplayName { get; set; }
    public DateTime? Stage1ApprovedAt { get; set; }
    public string? RejectedByDisplayName { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectReason { get; set; }

    /// <summary>True when this request needs Dynaway — single-stage, only the Dynaway-role
    /// approver acts (via CanComplete). False means the normal Stage1 (CanComplete) then Stage2
    /// (CanConfirmStage2) chain applies.</summary>
    public bool IsDynawayPath { get; set; }

    /// <summary>True for the Dynaway approver on a Dynaway request, or the Stage1 approver on any
    /// other request, while Status is Pending — fills out the form and either completes or rejects
    /// it. False for a viewer who can see this (an Administration Admin, for oversight) but isn't
    /// the right approver for the current stage.</summary>
    public bool CanComplete { get; set; }

    /// <summary>True for the Stage2 approver while Status is Stage1Approved — reviews what Stage1
    /// entered (read-only) and either confirms (creating the TDX ticket) or rejects. Never true on
    /// the Dynaway path (single-stage, no Stage2).</summary>
    public bool CanConfirmStage2 { get; set; }

    /// <summary>True for whichever approver is authorized at the CURRENT stage (Dynaway/Stage1
    /// while Pending, Stage2 while Stage1Approved) — declines the request outright, with a reason.</summary>
    public bool CanReject { get; set; }

    /// <summary>Same matched-approver rule as CanComplete/CanConfirmStage2 for the current stage,
    /// but ALSO true for an AppUsers Admin even when they aren't a matched approver — a safety net
    /// so a request nobody is matched to act on isn't permanently stuck with no one able to cancel
    /// it. Only ever true while Status is Pending or Stage1Approved.</summary>
    public bool CanCancel { get; set; }

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

public class CancelD365AccessApprovalDto
{
    /// <summary>Optional — shown alongside the cancellation in the Complétées/Annulées list, purely
    /// for context; nothing downstream depends on it.</summary>
    public string? Reason { get; set; }
}

/// <summary>A Stage1 or Stage2 approver actively declining the request — see
/// D365ApprovalStatus.Rejected. Unlike Cancel's optional reason, this one is required: the
/// requester is notified why, so a blank reason would be a dead-end email.</summary>
public class RejectD365AccessApprovalDto
{
    public string Reason { get; set; } = null!;
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

    /// <summary>The employee's Workday Cost_Center — the requester's starting point, but they may
    /// pick a different one from AdHocCostCenters (e.g. requesting D365 access scoped to a
    /// different department than the employee's own).</summary>
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

    /// <summary>Defaults to the employee's own Workday Cost_Center (see AdHocPrefill) when left
    /// blank — the requester picks from AdHocCostCenters (a dropdown of every distinct Cost_Center
    /// in Workday) only to override it.</summary>
    public string? DepartmentNumber { get; set; }

    /// <summary>Defaults to the employee's own Workday Manager when left blank — the requester
    /// picks a real AD account (AdHocAdSearch) only to override it, e.g. when the approval should
    /// route to someone other than the employee's literal Workday manager.</summary>
    public string? ManagerName { get; set; }

    /// <summary>"Besoin de gestion des actifs (Asset Management) avec Dynaway" checkbox — when
    /// true, TicketOrchestrationService.DynawayCommentDefault is prepended to Comments, same fixed
    /// wording the onboarding wizard's own Dynaway checkbox produces.</summary>
    public bool NeedsDynaway { get; set; }
}

public class SubmitAdHocD365AccessResultDto
{
    public int RequestId { get; set; }
    public string RequestNumber { get; set; } = null!;
}

/// <summary>The onboarding/réactivation wizard's own "D365 et Dynaway" step — same fields as
/// SubmitAdHocD365AccessDto, minus WorkdayEmployeeId (the wizard already knows the employee from
/// its own Employé step) and NeedsDynaway (the wizard's Dynaway checkbox already lives in
/// SubmitRequestDto.Applications; TryCreateD365AccessApprovalRequestAsync derives it from there,
/// same as it always has). Present on SubmitRequestDto only when "Accès D365" is one of the
/// selected Systemes — see RequestsController.Create's validation.</summary>
public class D365WizardDetailDto
{
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

    /// <summary>Defaults to the employee's own Workday Cost_Center when left blank — same
    /// override-or-default pattern as SubmitAdHocD365AccessDto.DepartmentNumber.</summary>
    public string? DepartmentNumber { get; set; }
}
