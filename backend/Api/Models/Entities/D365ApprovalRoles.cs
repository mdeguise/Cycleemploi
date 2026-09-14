namespace TremblantLifecycle.Api.Models.Entities;

/// <summary>The fixed routing roles for the D365 access approval workflow.
///
/// Dynaway/ProcurementStage1/ProcurementStage2/Other are the current model (see
/// D365ApprovalCategories): Dynaway and Other are each single-stage (sole approver fills and
/// finalizes); Procurement is two-stage (ProcurementStage1 fills, ProcurementStage2 confirms).
///
/// Stage1/Stage2 are the PREVIOUS generic model, kept only so approvals created before the
/// Dynaway/Procurement/Other redesign keep routing correctly (see D365AccessApproval.
/// ApprovalCategory being null) — never assigned to a new D365Approver going forward.</summary>
public static class D365ApprovalRoles
{
    /// <summary>Sole approver for any request where "Besoin de gestion des actifs (Asset
    /// Management) avec Dynaway" was checked — single-stage. Used by both the current and the
    /// legacy model, unchanged by the redesign.</summary>
    public const string Dynaway = "Dynaway";

    /// <summary>Legacy — first approver for every non-Dynaway request under the old generic model.
    /// Do not assign to new D365Approver rows; see the class doc comment.</summary>
    public const string Stage1 = "Stage1";

    /// <summary>Legacy — second and final approver for every non-Dynaway request under the old
    /// generic model. Do not assign to new D365Approver rows; see the class doc comment.</summary>
    public const string Stage2 = "Stage2";

    /// <summary>First approver for a request with at least one Procurement role checked — reviews/
    /// fills the form and either completes it (advancing to ProcurementStage2) or rejects it.</summary>
    public const string ProcurementStage1 = "ProcurementStage1";

    /// <summary>Second and final approver for a Procurement request — reviews what
    /// ProcurementStage1 filled in (read-only) and either confirms it (creating the TDX ticket) or
    /// rejects it.</summary>
    public const string ProcurementStage2 = "ProcurementStage2";

    /// <summary>Sole approver for any non-Dynaway request with no Procurement role checked —
    /// single-stage, same shape as Dynaway.</summary>
    public const string Other = "Other";

    public static readonly string[] All = [Dynaway, Stage1, Stage2, ProcurementStage1, ProcurementStage2, Other];

    /// <summary>Roles a D365Approver can newly be assigned — excludes the legacy Stage1/Stage2
    /// pair, which only ever appear on rows seeded before the redesign.</summary>
    public static readonly string[] Assignable = [Dynaway, ProcurementStage1, ProcurementStage2, Other];

    public static bool IsValid(string? role) => role is not null && All.Contains(role);
}

/// <summary>The three routing categories a new (post-redesign) D365AccessApproval falls into —
/// see D365AccessApproval.ApprovalCategory. Determine() is the single place this decision is
/// made, called at creation time by both TicketOrchestrationService.
/// TryCreateD365AccessApprovalRequestAsync (the wizard) and D365AccessApprovalsController.
/// SubmitAdHoc (the standalone app), so the two entry points can never disagree.</summary>
public static class D365ApprovalCategories
{
    public const string Dynaway = "Dynaway";
    public const string Procurement = "Procurement";
    public const string Other = "Other";

    /// <summary>Dynaway wins outright regardless of roles (matches the frontend auto-checking
    /// Accès D365 whenever Dynaway is selected). Otherwise Procurement if ANY selected role starts
    /// with "Procurement" (TdxD365RoleCheckboxes' own prefix, e.g. "Procurement - Approver/
    /// Requester") — even alongside other, non-Procurement roles, since Procurement approvers still
    /// need to review it. Everything else is Other.</summary>
    public static string Determine(bool needsDynaway, IEnumerable<string> roles) =>
        needsDynaway
            ? Dynaway
            : roles.Any(r => r.StartsWith("Procurement", StringComparison.OrdinalIgnoreCase))
                ? Procurement
                : Other;

    /// <summary>Only Procurement (new model) and null (legacy non-Dynaway) ever reach a second
    /// stage — Dynaway and Other are single-stage. Shared by D365AccessApprovalsController's
    /// CurrentStageRole and TicketOrchestrationService.NotifyD365Stage2ApproversAsync so the two
    /// can never disagree about who the second-stage approver is.</summary>
    public static string Stage2Role(string? category) => category switch
    {
        Procurement => D365ApprovalRoles.ProcurementStage2,
        _ => D365ApprovalRoles.Stage2
    };
}
