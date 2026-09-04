namespace TremblantLifecycle.Api.Services;

/// <summary>ApiKey is deliberately left empty here — the real value lives in
/// appsettings.Production.json on the server only, never committed to source control. See
/// CONTRIBUTING.md.</summary>
public class FreshdeskOptions
{
    public string Subdomain { get; set; } = "";
    public string ApiKey { get; set; } = "";

    /// <summary>"RH - Général" — the main ticket created for every submission.</summary>
    public long GroupId { get; set; }
    public long EmailConfigId { get; set; }
    public string TicketType { get; set; } = "";

    /// <summary>"RH - Horaires" (payroll/scheduling) — an independent Freshdesk ticket created
    /// alongside the main one on every submission, including the employee's full job-code history
    /// (unlike RedingoteGroupId's ticket). Confirmed against the real tremblantsmt.freshdesk.com
    /// instance's /api/v2/groups. Not a Freshdesk parent-child relationship (no parent_id) — each
    /// fanned-out ticket is its own independent ticket, correlated only by sharing the same subject
    /// text and request number.</summary>
    public long HorairesGroupId { get; set; }

    /// <summary>"RH - Redingote" (uniforms/équipement) — same fan-out pattern as HorairesGroupId.</summary>
    public long RedingoteGroupId { get; set; }

    /// <summary>"SAC - ISAC" (stationnement) — same fan-out pattern as HorairesGroupId.</summary>
    public long StationnementGroupId { get; set; }
}
