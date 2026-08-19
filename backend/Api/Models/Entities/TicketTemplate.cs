namespace TremblantLifecycle.Api.Models.Entities;

/// <summary>Admin-editable content for one of the free-text pieces a submission's ticket
/// integrations send (Freshdesk main/child tickets, TDX Quick Incident) — see
/// TicketTemplateDefaults for the fixed catalog of Keys, their default Content, and which
/// placeholders each one supports. Content uses {{PlaceholderName}} tokens, substituted at
/// send-time by TicketTemplateRenderer; a placeholder with no value for a given submission renders
/// as "—", matching how empty fields are shown everywhere else in this app's UI.</summary>
public class TicketTemplate
{
    public int TicketTemplateId { get; set; }

    /// <summary>Matches one of TicketTemplateDefaults.All's Keys — not a free-form admin-chosen
    /// name.</summary>
    public string Key { get; set; } = null!;

    public string Content { get; set; } = null!;
    public DateTime UpdatedAt { get; set; }
    public string? UpdatedByDisplayName { get; set; }
}
