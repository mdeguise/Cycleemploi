namespace TremblantLifecycle.Api.Services;

/// <summary>Whether a ticket is still being worked on, normalized across two systems that model it
/// completely differently. Freshdesk uses a numeric status (2 Open, 3 Pending, 4 Resolved,
/// 5 Closed); TDX uses a StatusClass (New / InProcess / OnHold / Completed / Cancelled). The
/// Administration list only cares about the distinction a human cares about.</summary>
public enum LiveTicketState
{
    /// <summary>The lookup failed, timed out, or returned a status we do not recognise. Deliberately
    /// distinct from Closed — reporting an unreachable ticket as "closed" would be a lie, and the
    /// kind of lie someone acts on.</summary>
    Unknown = 0,
    Open = 1,
    Closed = 2
}

/// <summary><paramref name="Label"/> is the source system's own wording ("Pending", "In Process"),
/// kept so the screen can show what the system actually says rather than only our two-way
/// summary.</summary>
public record LiveTicketStatus(LiveTicketState State, string? Label)
{
    public static readonly LiveTicketStatus Unknown = new(LiveTicketState.Unknown, null);
}
