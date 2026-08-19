namespace TremblantLifecycle.Api.Models.Entities;

/// <summary>Admin-editable content for one of the pieces a submission's ticket integrations send
/// (Freshdesk main/child tickets, TDX Quick Incident title/description) — see
/// TicketTemplateDefaults for the fixed catalog of Keys and their default Content. Content is JSON
/// (InlineTemplateContent or BlockTemplateContent, per the Key's TicketTemplateShape), built and
/// edited via the structured admin UI, never raw HTML or {{placeholder}} text — the admin never
/// sees template syntax. Rendered at send-time by TicketTemplateRenderer.</summary>
public class TicketTemplate
{
    public int TicketTemplateId { get; set; }

    /// <summary>Matches one of TicketTemplateDefaults.All's Keys — not a free-form admin-chosen
    /// name.</summary>
    public string Key { get; set; } = null!;

    /// <summary>JSON — see the class doc comment.</summary>
    public string Content { get; set; } = null!;
    public DateTime UpdatedAt { get; set; }
    public string? UpdatedByDisplayName { get; set; }
}
