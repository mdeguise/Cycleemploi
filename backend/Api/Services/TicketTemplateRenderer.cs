using System.Text;
using System.Text.Json;
using TremblantLifecycle.Api.Models.Entities;

namespace TremblantLifecycle.Api.Services;

/// <summary>Renders a TicketTemplate's structured Content JSON (Inline or Block shape — see
/// TicketTemplateShape) into the actual ticket text. No HTML-encoding — matches the ticket-building
/// code this replaced, which never encoded field values either. A missing/blank value renders as
/// "—", matching how empty fields are shown everywhere else in this app's UI.</summary>
public static class TicketTemplateRenderer
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static string ValueOrDash(IReadOnlyDictionary<string, string?> values, string? key)
    {
        if (key is null) return "—";
        return values.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v! : "—";
    }

    public static string RenderInline(string contentJson, IReadOnlyDictionary<string, string?> values)
    {
        var content = JsonSerializer.Deserialize<InlineTemplateContent>(contentJson, JsonOptions) ?? new InlineTemplateContent();
        var sb = new StringBuilder();
        foreach (var part in content.Parts)
        {
            if (part.Type == "text")
            {
                sb.Append(part.Text);
            }
            else if (part.Type == "field")
            {
                sb.Append(ValueOrDash(values, part.FieldKey));
            }
        }
        return sb.ToString();
    }

    /// <param name="requestValues">Request-level field values (see TicketTemplateField's
    /// TicketFieldCategory.Request).</param>
    /// <param name="employeeValuesList">One dictionary per employee on the request, in order — an
    /// "employeeGroup" block renders once per entry.</param>
    public static string RenderBlock(
        string contentJson,
        IReadOnlyDictionary<string, string?> requestValues,
        IReadOnlyList<IReadOnlyDictionary<string, string?>> employeeValuesList)
    {
        var content = JsonSerializer.Deserialize<BlockTemplateContent>(contentJson, JsonOptions) ?? new BlockTemplateContent();
        var sb = new StringBuilder();
        var paragraphOpen = false;

        void OpenParagraph()
        {
            if (!paragraphOpen)
            {
                sb.Append("<p>");
                paragraphOpen = true;
            }
        }

        void CloseParagraph()
        {
            if (paragraphOpen)
            {
                sb.Append("</p>");
                paragraphOpen = false;
            }
        }

        foreach (var block in content.Blocks)
        {
            switch (block.Type)
            {
                case "heading":
                    CloseParagraph();
                    sb.Append("<h4>").Append(block.HeadingText).Append("</h4>");
                    break;

                case "field":
                    OpenParagraph();
                    sb.Append("<b>").Append(block.Label).Append(":</b> ").Append(ValueOrDash(requestValues, block.FieldKey)).Append("<br>");
                    break;

                case "employeeGroup":
                    CloseParagraph();
                    if (!string.IsNullOrWhiteSpace(block.EmployeeGroupHeading))
                    {
                        sb.Append("<h4>").Append(block.EmployeeGroupHeading).Append("</h4>");
                    }
                    foreach (var employeeValues in employeeValuesList)
                    {
                        sb.Append("<p>");
                        foreach (var line in block.EmployeeFields)
                        {
                            sb.Append("<b>").Append(line.Label).Append(":</b> ").Append(ValueOrDash(employeeValues, line.FieldKey)).Append("<br>");
                        }
                        sb.Append("</p>");
                    }
                    break;
            }
        }

        CloseParagraph();
        return sb.ToString();
    }
}
