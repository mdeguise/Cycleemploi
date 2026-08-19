namespace TremblantLifecycle.Api.Models.Dtos;

public class TicketTemplateFieldDto
{
    public string Key { get; set; } = null!;
    public string Label { get; set; } = null!;
}

public class TicketTemplateDto
{
    public string Key { get; set; } = null!;
    public string Label { get; set; } = null!;
    public string Description { get; set; } = null!;

    /// <summary>"Inline" or "Block" — see TicketTemplateShape. Drives which editor UI the frontend
    /// shows.</summary>
    public string Shape { get; set; } = null!;

    /// <summary>JSON — InlineTemplateContent or BlockTemplateContent, per Shape.</summary>
    public string Content { get; set; } = null!;
    public string DefaultContent { get; set; } = null!;

    public List<TicketTemplateFieldDto> RequestFields { get; set; } = [];

    /// <summary>Empty when this template's Shape doesn't support employee fields (never happens
    /// today — every current template does — kept for forward compatibility).</summary>
    public List<TicketTemplateFieldDto> EmployeeFields { get; set; } = [];

    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedByDisplayName { get; set; }
}

public class UpdateTicketTemplateDto
{
    /// <summary>JSON — must parse as the Key's shape (InlineTemplateContent or
    /// BlockTemplateContent); validated server-side.</summary>
    public string Content { get; set; } = null!;
}
