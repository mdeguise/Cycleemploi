using TremblantLifecycle.Api.Models.Entities;

namespace TremblantLifecycle.Api.Services;

/// <summary>Reads/writes the admin-editable ticket content templates — see TicketTemplateDefaults
/// for the fixed catalog of Keys this operates over.</summary>
public interface ITicketTemplateService
{
    /// <returns>The current content for the given Key — the DB row if one exists, otherwise the
    /// built-in default (defensive fallback; every Key should have a seeded row).</returns>
    Task<string> GetContentAsync(string key, CancellationToken ct);

    Task<List<TicketTemplate>> ListAsync(CancellationToken ct);

    /// <exception cref="ArgumentException">Key isn't in TicketTemplateDefaults.ByKey.</exception>
    Task<TicketTemplate> UpdateAsync(string key, string content, string? updatedByDisplayName, CancellationToken ct);
}
