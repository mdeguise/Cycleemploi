namespace TremblantLifecycle.Api.Models.Dtos;

public class TicketTemplatePlaceholderDto
{
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
}

public class TicketTemplateDto
{
    public string Key { get; set; } = null!;
    public string Label { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Content { get; set; } = null!;
    public string DefaultContent { get; set; } = null!;
    public List<TicketTemplatePlaceholderDto> Placeholders { get; set; } = [];
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedByDisplayName { get; set; }
}

public class UpdateTicketTemplateDto
{
    public string Content { get; set; } = null!;
}
