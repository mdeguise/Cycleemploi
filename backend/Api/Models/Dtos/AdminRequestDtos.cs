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

/// <summary>One row in the Administration request list. Deliberately lighter than the full request:
/// the list only needs enough to scan and to spot which requests need attention.</summary>
public class AdminRequestSummaryDto
{
    public int RequestId { get; set; }
    public string RequestNumber { get; set; } = null!;
    public string RequestType { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string DemandePar { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public List<string> EmployeeNames { get; set; } = [];

    public int TicketsCreated { get; set; }

    /// <summary>Drives the "à corriger" badge — the reason this screen exists.</summary>
    public int TicketsFailed { get; set; }
}

public class AdminRequestListDto
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<AdminRequestSummaryDto> Items { get; set; } = [];
}

public class AdminRequestDetailDto
{
    /// <summary>Everything the requester entered — the same DTO the wizard renders, so an
    /// administrator reviews exactly what was submitted rather than an approximation.</summary>
    public RequestDto Request { get; set; } = null!;

    /// <summary>The address downstream tickets are raised under. Null on requests submitted before
    /// this was captured, which is also why those cannot be retried.</summary>
    public string? RequesterEmail { get; set; }

    public DateTime? SubmittedAt { get; set; }

    /// <summary>False for a Lecteur — the UI hides the Réessayer buttons, and the API refuses them
    /// regardless.</summary>
    public bool CanRetry { get; set; }

    public List<RequestTicketDto> Tickets { get; set; } = [];
}
