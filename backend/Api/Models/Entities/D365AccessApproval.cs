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

    // ---- Filled in by the approver when they complete the form ----

    /// <summary>English translation of the position title — the TDX form must be entirely in
    /// English and Workday's Position_Title is French-only at Tremblant. Pre-filled with the French
    /// title as a starting point; the approver is expected to translate it.</summary>
    public string? JobTitleEnglish { get; set; }

    public string? LegalEntity { get; set; }
    public string? DepartmentNumber { get; set; }
    public decimal? ApprovalLimit { get; set; }
    public string? ApAccessDetails { get; set; }
    public string? AdditionalLegalEntities { get; set; }
    public bool? LevyEmployee { get; set; }

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
