namespace TremblantLifecycle.Api.Models.Entities;

/// <summary>Structural shape of a template's Content JSON — drives which editor UI (and renderer)
/// applies. See TicketTemplateDefaults' doc comment for the full design rationale.</summary>
public enum TicketTemplateShape
{
    /// <summary>An ordered chain of static-text/field parts, concatenated with no HTML — used for
    /// Freshdesk ticket subjects and TDX titles/descriptions.</summary>
    Inline,

    /// <summary>An ordered list of section headings, single field-lines, and "employee group"
    /// blocks (which repeat once per employee on the request) — rendered as HTML — used for
    /// Freshdesk ticket bodies.</summary>
    Block
}

/// <summary>Which set of fields a TicketTemplateField belongs to, so the admin UI only offers
/// employee-specific fields (Poste, Gestionnaire, Date d'embauche, ...) inside an "employee group"
/// block or a TDX inline template (which always concerns exactly one employee), never as a
/// request-level line in a Freshdesk body (where a termination can list several employees and a
/// single employee-level value would be ambiguous).</summary>
public enum TicketFieldCategory
{
    Request,
    Employee
}

public record TicketTemplateField(string Key, string Label, TicketFieldCategory Category);

// ---- Inline shape ----

public class InlinePart
{
    /// <summary>"field" or "text".</summary>
    public string Type { get; set; } = null!;

    /// <summary>Set when Type == "field" — one of the template's available TicketTemplateField
    /// Keys.</summary>
    public string? FieldKey { get; set; }

    /// <summary>Set when Type == "text" — literal text the admin typed (e.g. " - ", " (#", ")").</summary>
    public string? Text { get; set; }
}

public class InlineTemplateContent
{
    public List<InlinePart> Parts { get; set; } = [];
}

// ---- Block shape ----

public class EmployeeFieldLine
{
    public string Label { get; set; } = null!;
    public string FieldKey { get; set; } = null!;
}

public class TemplateBlock
{
    /// <summary>"heading", "field", or "employeeGroup".</summary>
    public string Type { get; set; } = null!;

    /// <summary>Set when Type == "heading" — static section title text, no dynamic fields.</summary>
    public string? HeadingText { get; set; }

    /// <summary>Set when Type == "field" — a single request-level "Label: value" line.</summary>
    public string? Label { get; set; }

    public string? FieldKey { get; set; }

    /// <summary>Set when Type == "employeeGroup" — an optional heading shown once before the
    /// repeated employee blocks, and the ordered list of "Label: value" lines rendered once per
    /// employee on the request (so a single-employee onboarding renders it once, a multi-employee
    /// termination renders it once per person, with no loop logic exposed to the admin).</summary>
    public string? EmployeeGroupHeading { get; set; }

    public List<EmployeeFieldLine> EmployeeFields { get; set; } = [];
}

public class BlockTemplateContent
{
    public List<TemplateBlock> Blocks { get; set; } = [];
}
