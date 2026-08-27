using TremblantLifecycle.Api.Models.Entities;

namespace TremblantLifecycle.Api.Models.Dtos;

/// <summary>French labels for the ticket kinds, kept server-side so the API and any future export
/// agree on wording rather than each surface inventing its own.</summary>
public static class TicketKindLabels
{
    public static string For(TicketKind kind) => kind switch
    {
        TicketKind.Freshdesk => "Freshdesk — RH Général",
        TicketKind.FreshdeskChildWithJobCodes => "Freshdesk — billet enfant (avec codes d'emploi)",
        TicketKind.FreshdeskChildWithoutJobCodes => "Freshdesk — billet enfant (sans codes d'emploi)",
        TicketKind.Tdx => "TDX — OneIT",
        TicketKind.D365Badge => "D365 — badge / alarme",
        TicketKind.D365Access => "TDX — accès D365",
        _ => kind.ToString()
    };
}

public class RequestTicketDto
{
    public int RequestTicketId { get; set; }

    /// <summary>Enum name, for the UI to switch on.</summary>
    public string Kind { get; set; } = null!;

    /// <summary>Human-readable French label for the same thing.</summary>
    public string KindLabel { get; set; } = null!;

    /// <summary>"Created" or "Failed".</summary>
    public string Outcome { get; set; } = null!;

    /// <summary>Null for request-level tickets (the Freshdesk parent and its children).</summary>
    public int? RequestEmployeeId { get; set; }
    public string? EmployeeName { get; set; }

    public string? TicketNumber { get; set; }
    public string? ErrorType { get; set; }
    public string? ErrorMessage { get; set; }
    public int AttemptCount { get; set; }
    public DateTime FirstAttemptAt { get; set; }
    public DateTime LastAttemptAt { get; set; }
}

public class RetryTicketResultDto
{
    public bool Succeeded { get; set; }
    public string? TicketNumber { get; set; }
    public string? Error { get; set; }
}
