using Microsoft.EntityFrameworkCore;
using TremblantLifecycle.Api.Data;
using TremblantLifecycle.Api.Models.Entities;

namespace TremblantLifecycle.Api.Services;

public class TicketTemplateService : ITicketTemplateService
{
    private readonly AppDbContext _db;

    public TicketTemplateService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<string> GetContentAsync(string key, CancellationToken ct)
    {
        var content = await _db.TicketTemplates
            .Where(t => t.Key == key)
            .Select(t => t.Content)
            .FirstOrDefaultAsync(ct);

        return content ?? TicketTemplateDefaults.ByKey[key].DefaultContent;
    }

    public async Task<List<TicketTemplate>> ListAsync(CancellationToken ct) =>
        await _db.TicketTemplates.OrderBy(t => t.Key).ToListAsync(ct);

    public async Task<TicketTemplate> UpdateAsync(string key, string content, string? updatedByDisplayName, CancellationToken ct)
    {
        if (!TicketTemplateDefaults.ByKey.ContainsKey(key))
        {
            throw new ArgumentException($"Unknown ticket template key '{key}'.", nameof(key));
        }

        var template = await _db.TicketTemplates.FirstOrDefaultAsync(t => t.Key == key, ct);
        if (template is null)
        {
            template = new TicketTemplate { Key = key };
            _db.TicketTemplates.Add(template);
        }

        template.Content = content;
        template.UpdatedAt = DateTime.UtcNow;
        template.UpdatedByDisplayName = updatedByDisplayName;

        await _db.SaveChangesAsync(ct);
        return template;
    }
}
