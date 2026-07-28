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

    public int WorkdayEmployeeId { get; set; }
    public string NameSnapshot { get; set; } = null!;
    public string? PositionSnapshot { get; set; }
    public string? DepartementSnapshot { get; set; }
    public string? CodeEmploiSnapshot { get; set; }
    public string? TypeEmploiSnapshot { get; set; }
    public string? GestionnaireSnapshot { get; set; }
    public bool IsPrimary { get; set; }
}
