namespace TremblantLifecycle.Api.Models.Entities;

public enum D365ApprovalStatus
{
    /// <summary>Created at submit time when "Accès D365" was requested. No approver has filled out
    /// the form yet — this is what the tracking screen calls "pending".</summary>
    Pending = 0,

    /// <summary>An approver filled out the form and pressed Envoyer. Whether the resulting TDX
    /// ticket call itself succeeded is tracked separately, the same way as every other integration
    /// in this app — see RequestTicket(Kind=D365Access) for that outcome. A Completed approval whose
    /// ticket failed is retried through the normal Administration/Réessayer path, not by reopening
    /// this form to the approver.</summary>
    Completed = 1
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
    public string? CompletedByObjectId { get; set; }
    public string? CompletedByDisplayName { get; set; }
    public DateTime? CompletedAt { get; set; }
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
