namespace TremblantLifecycle.Api.Models.Entities;

/// <summary>Which downstream system a <see cref="RequestTicket"/> row represents. Stored as a string
/// so the table is readable to a DBA and so inserting a new integration later cannot renumber the
/// existing ones.
///
/// Three of these are request-level (one row per request) and three are per-employee (one row per
/// employee on the request), because a single termination can target several people at once. See
/// <see cref="RequestTicket.RequestEmployeeId"/>.</summary>
public enum TicketKind
{
    /// <summary>Request-level. The main Freshdesk ticket, in the "RH - Général" queue.</summary>
    Freshdesk = 0,

    /// <summary>Request-level. Independent Freshdesk ticket fanned out to "RH - Horaires"
    /// (group id, see FreshdeskOptions.HorairesGroupId), the payroll/scheduling group that needs
    /// the employee's full job-code history. Member NAME kept as-is (originally "Freshdesk child
    /// ticket") even though it's no longer a Freshdesk parent-child relationship — this is a
    /// persisted string value (see AppDbContext's HasConversion&lt;string&gt;), so renaming it would
    /// break every existing RequestTicket row already using this name.</summary>
    FreshdeskChildWithJobCodes = 1,

    /// <summary>Request-level. Independent Freshdesk ticket fanned out to "RH - Redingote" (group
    /// id, see FreshdeskOptions.RedingoteGroupId), the uniforms/équipement department. Same
    /// never-rename-this-member-name note as FreshdeskChildWithJobCodes applies.</summary>
    FreshdeskChildWithoutJobCodes = 2,

    /// <summary>Per-employee. TDX ticket in the "OneIT" application.</summary>
    Tdx = 3,

    /// <summary>Per-employee. D365 Enterprise Asset Management badge/alarm request, raised through
    /// the Power Automate webhook. Records the returned D365 job code rather than a ticket id.</summary>
    D365Badge = 4,

    /// <summary>Per-employee. TDX ticket on the D365 Access form (FinApp Triage).</summary>
    D365Access = 5,

    /// <summary>Request-level. Independent Freshdesk ticket fanned out to "SAC - ISAC" (group id,
    /// see FreshdeskOptions.StationnementGroupId), the parking department.</summary>
    FreshdeskStationnement = 6
}

public enum TicketOutcome
{
    /// <summary>The downstream system accepted it. <see cref="RequestTicket.TicketNumber"/> holds
    /// whatever identifier it returned.</summary>
    Created = 0,

    /// <summary>The attempt threw. <see cref="RequestTicket.ErrorMessage"/> holds why, and this row
    /// is what the Réessayer button in the Administration screen acts on.</summary>
    Failed = 1
}

/// <summary>The outcome of one attempt to create one downstream ticket for one request.
///
/// This exists because the app previously persisted NOTHING about its integrations: when Freshdesk
/// and TDX both failed on request #INT-2026-00053, the only record anywhere was two notification
/// emails, and there was no way for the app to know which of its four integrations had actually
/// fired. That made a safe retry impossible — re-submitting would have duplicated the two that
/// succeeded.
///
/// A row is written on success AND on failure. The unique index over
/// (RequestId, Kind, RequestEmployeeId) is what makes retry idempotent: a retry updates the
/// existing row rather than creating a second ticket. SQL Server treats NULLs as equal in a unique
/// index, so the request-level kinds are correctly limited to one row each per request.</summary>
public class RequestTicket
{
    public int RequestTicketId { get; set; }

    public int RequestId { get; set; }
    public Request Request { get; set; } = null!;

    /// <summary>The employee this ticket is for, or null for request-level kinds (the main
    /// Freshdesk ticket and every independent Freshdesk ticket fanned out alongside it).</summary>
    public int? RequestEmployeeId { get; set; }
    public RequestEmployee? RequestEmployee { get; set; }

    public TicketKind Kind { get; set; }
    public TicketOutcome Outcome { get; set; }

    /// <summary>The identifier the downstream system returned — a string because the systems
    /// disagree: Freshdesk returns a long, TDX an int, and the D365 badge integration returns a job
    /// code (or a placeholder when the webhook accepts the request without returning an id).
    /// Null when <see cref="Outcome"/> is Failed.</summary>
    public string? TicketNumber { get; set; }

    /// <summary>Exception type name, kept separate from the message so the Administration screen can
    /// group recurring failures without parsing prose.</summary>
    public string? ErrorType { get; set; }

    public string? ErrorMessage { get; set; }

    /// <summary>Counts every attempt including the first, so a row that keeps failing is visible as
    /// a persistent problem rather than a one-off.</summary>
    public int AttemptCount { get; set; }

    public DateTime FirstAttemptAt { get; set; }
    public DateTime LastAttemptAt { get; set; }
}
