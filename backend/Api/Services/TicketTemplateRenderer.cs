using System.Text.RegularExpressions;

namespace TremblantLifecycle.Api.Services;

/// <summary>Renders a TicketTemplate's Content by substituting {{Placeholder}} tokens with values
/// from a lookup — no HTML-encoding, matching the ticket-building code this replaced (which never
/// encoded field values either), so switching a ticket type over to templates doesn't change how
/// special characters in e.g. a position title show up. A missing/blank value renders as "—",
/// matching how empty fields are shown everywhere else in this app's UI.</summary>
public static partial class TicketTemplateRenderer
{
    public static string Render(string template, IReadOnlyDictionary<string, string?> values)
    {
        return PlaceholderPattern().Replace(template, m =>
        {
            var key = m.Groups[1].Value;
            return values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : "—";
        });
    }

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex PlaceholderPattern();
}
