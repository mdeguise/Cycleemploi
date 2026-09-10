namespace TremblantLifecycle.Api.Models.Entities;

public enum D365ApprovalStatus
{
    /// <summary>Created at submit time when "Accès D365" was requested. For a Dynaway request,
    /// awaiting the sole Dynaway approver. For every other request, awaiting a Stage1 approver to
    /// fill out the form — this is what the tracking screen calls "pending".</summary>
    Pending = 0,

    /// <summary>An approver filled out the form and pressed Envoyer. Whether the resulting TDX
    /// ticket call itself succeeded is tracked separately, the same way as every other integration
    /// in this app — see RequestTicket(Kind=D365Access) for that outcome. A Completed approval whose
    /// ticket failed is retried through the normal Administration/Réessayer path, not by reopening
    /// this form to the approver.</summary>
    Completed = 1,

    /// <summary>A Stage1/Dynaway approver (or an AppUsers Admin, as a safety net) decided this
    /// request should not proceed at all — no TDX ticket is ever created. Terminal: never becomes
    /// Pending/Stage1Approved/Completed again. Distinct from Rejected, which is a Stage1/Stage2
    /// approver actively saying no (with a reason) rather than this administrative withdrawal.</summary>
    Cancelled = 2,

    /// <summary>Non-Dynaway requests only — a Stage1 approver has filled out and completed the
    /// form; awaiting a Stage2 approver's final confirmation (read-only review, no re-entry) before
    /// the TDX ticket is created. Never reached on the Dynaway path, which is single-stage.</summary>
    Stage1Approved = 3,

    /// <summary>A Stage1 or Stage2 approver actively declined the request (see RejectReason) —
    /// terminal, no TDX ticket is ever created. The requester is notified why.</summary>
    Rejected = 4
}

/// <summary>One row per (Onboarding/Réactivation) request that requested "Accès D365" — the
/// reactive replacement for the old per-job-code D365JobCodeTemplate: instead of an admin
/// pre-filling a template before anyone needs it (which in practice never happened — the table
/// shipped and stayed empty), the specific fields a D365 access grant actually requires (roles,
/// approval limit, legal entity, ...) are collected once, from a matched approver, at the moment a
/// real request needs them.
///
/// 1:1 with Request — D365 Access only ever concerns the single primary employee on an
/// Onboarding/Réactivation request (never Offboarding, never more than one person), matching the
/// existing gating in TicketOrchestrationService.</summary>
public class D365AccessApproval
{
    public int RequestId { get; set; }
    public Request Request { get; set; } = null!;

    /// <summary>The employee this concerns — kept explicit (rather than always re-deriving "the
    /// primary employee") so a reader doesn't have to re-trace that rule to know who a row is for.</summary>
    public int RequestEmployeeId { get; set; }

    public D365ApprovalStatus Status { get; set; } = D365ApprovalStatus.Pending;

    /// <summary>Set once at creation time (from the wizard's Dynaway checkbox, or the standalone
    /// ad-hoc app's own — see TryCreateD365AccessApprovalRequestAsync/SubmitAdHoc) and never
    /// changed after — decides the whole routing question: true routes to the sole Dynaway-role
    /// approver (single stage); false routes through Stage1 then Stage2. Stored directly here
    /// rather than re-derived from Request.ApplicationsDetail each time, since an ad-hoc request
    /// (RequestType.D365AccessOnly) never populates ApplicationsDetail at all.</summary>
    public bool NeedsDynaway { get; set; }

    /// <summary>"New Access" | "Change Access" | "Remove Access" — the real TDX form's own
    /// wording, matched exactly so the value can be sent straight through (see
    /// D365AccessApprovalsController.AllowedAccessTypes). Null on every approval created by the
    /// wizard-driven onboarding flow (that path never asked — it's always a new hire getting D365
    /// for the first time); D365AccessTicketInput/TdxService fall back to "New Access" when null,
    /// preserving that flow's existing behavior unchanged. Ad-hoc requests from the standalone
    /// D365AccessRequest app always set it explicitly — that app can request any of the three.</summary>
    public string? AccessType { get; set; }

    // ---- Filled in by the approver when they complete the form ----

    /// <summary>English translation of the position title — the TDX form must be entirely in
    /// English and Workday's Position_Title is French-only at Tremblant. Pre-filled from Workday's
    /// own Job_Profile (already English) joined with Position_Title as a starting point; the
    /// approver may edit it.</summary>
    public string? JobTitleEnglish { get; set; }

    /// <summary>Fixed at "6201" for every request — see
    /// D365AccessApprovalsController.FixedLegalEntity — set server-side, never an approver input.
    /// Still a real column (not a hardcoded literal at the TDX call site) so it's visible in the
    /// saved record for audit.</summary>
    public string? LegalEntity { get; set; }

    /// <summary>Always the employee's Workday Cost_Center, resolved server-side at completion time
    /// — never an approver input, same reasoning as LegalEntity.</summary>
    public string? DepartmentNumber { get; set; }

    public decimal? ApprovalLimit { get; set; }
    public string? ApAccessDetails { get; set; }
    public string? AdditionalLegalEntities { get; set; }

    /// <summary>Confirmed as a real row on the TDX form (between AP Access Details and Approval
    /// Limit) via a real historical "D365 - Access" ticket — not on the form for every request
    /// (only appeared once, for a Procurement-flavoured role), so kept optional-if-present, same
    /// pattern as ApAccessDetails/AdditionalLegalEntities.</summary>
    public string? DefaultShippingAddress { get; set; }

    public bool? LevyEmployee { get; set; }

    /// <summary>Free-text comments from the approver — included on the TDX ticket (as "Additional
    /// Details or Comments", the real field's label) only when non-empty, same pattern as
    /// ApAccessDetails/AdditionalLegalEntities.</summary>
    public string? Comments { get; set; }

    public ICollection<D365AccessApprovalRole> Roles { get; set; } = new List<D365AccessApprovalRole>();

    public DateTime CreatedAt { get; set; }

    /// <summary>Set by Stage1 completing the form (non-Dynaway path only) — recorded separately
    /// from CompletedBy* because the FINAL sign-off on a two-stage request is Stage2's, not
    /// Stage1's. Null on the Dynaway path (single-stage — CompletedBy* covers it) and null until
    /// Stage1 acts.</summary>
    public string? Stage1ApprovedByObjectId { get; set; }
    public string? Stage1ApprovedByDisplayName { get; set; }
    public DateTime? Stage1ApprovedAt { get; set; }

    /// <summary>The FINAL approval: the Dynaway approver on a Dynaway request, or the Stage2
    /// approver's confirmation on every other request.</summary>
    public string? CompletedByObjectId { get; set; }
    public string? CompletedByDisplayName { get; set; }
    public DateTime? CompletedAt { get; set; }

    public string? CancelledByObjectId { get; set; }
    public string? CancelledByDisplayName { get; set; }
    public DateTime? CancelledAt { get; set; }

    /// <summary>Optional free-text — why this request was cancelled, for whoever looks at it later
    /// (the original requester has no other way to find out).</summary>
    public string? CancelReason { get; set; }

    /// <summary>Set when a Stage1 or Stage2 approver actively declines the request (see
    /// D365ApprovalStatus.Rejected) — distinct from Cancel, which is an administrative withdrawal
    /// rather than an approver's own "no".</summary>
    public string? RejectedByObjectId { get; set; }
    public string? RejectedByDisplayName { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectReason { get; set; }
}

/// <summary>A single checked role on a completed D365AccessApproval — same free-text vocabulary as
/// the old D365JobCodeTemplateRole (the fixed TdxD365RoleCheckboxes.All catalog).</summary>
public class D365AccessApprovalRole
{
    public int Id { get; set; }
    public int RequestId { get; set; }
    public D365AccessApproval D365AccessApproval { get; set; } = null!;
    public string Role { get; set; } = null!;
}
