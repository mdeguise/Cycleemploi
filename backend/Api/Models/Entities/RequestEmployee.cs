namespace TremblantLifecycle.Api.Models.Entities;

/// <summary>Denormalized snapshot of the employee(s) on a request, captured at selection time —
/// deliberately NOT a live FK into WorkdayDemographic, since that table reloads on its own hourly
/// schedule outside this app's control. One row for onboarding/réactivation, several for
/// offboarding. Keeps historical requests stable against later Workday corrections.</summary>
public class RequestEmployee
{
    public int RequestEmployeeId { get; set; }
    public int RequestId { get; set; }
    public Request Request { get; set; } = null!;

    public string WorkdayEmployeeId { get; set; } = null!;
    public string NameSnapshot { get; set; } = null!;
    public string? PositionSnapshot { get; set; }
    public string? DepartementSnapshot { get; set; }
    public string? CodeEmploiSnapshot { get; set; }
    public string? TypeEmploiSnapshot { get; set; }
    public string? GestionnaireSnapshot { get; set; }
    public bool IsPrimary { get; set; }

    /// <summary>RÉACTIVATION only: the position Workday itself showed when the request was submitted (server-read,
    /// never client-supplied). PositionSnapshot is what the manager asked for ("poste de retour"); when the two
    /// differ the tickets say so. Null for other request types and for requests older than this column.</summary>
    public string? PositionWorkdayAtSubmit { get; set; }

    /// <summary>Last Workday primary position the nightly follow-up (PositionWatchService) has already reported
    /// on the TDX ticket, so a change is announced once.</summary>
    public string? PositionLastFollowed { get; set; }
}
