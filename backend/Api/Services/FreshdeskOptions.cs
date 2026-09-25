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

    /// <summary>Ticket Type for the SAC - ISAC ticket — every other ticket sends TicketType
    /// ("RH - Général"), which is wrong for a ticket in a SAC group. Freshdesk's Type list has no
    /// entry named after the group; "SAC Demande stationnement interne" is the internal-parking one.</summary>
    public string StationnementTicketType { get; set; } = "SAC Demande stationnement interne";

    /// <summary>Freshdesk support email "Tremblant Expérience Invité" (sac@tremblantsmt.freshdesk.com),
    /// whose product is Tremblant Expérience Invité — a ticket's product comes from its
    /// email_config_id, and EmailConfigId (RH - Général) would file this one under Tremblant RH.</summary>
    public long StationnementEmailConfigId { get; set; } = 154000026832;
}
