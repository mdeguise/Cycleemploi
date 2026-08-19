using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TremblantLifecycle.Api.Models.Dtos;
using TremblantLifecycle.Api.Models.Entities;
using TremblantLifecycle.Api.Services;

namespace TremblantLifecycle.Api.Controllers;

/// <summary>Lets a Ticket Template admin (see AppUser) view and edit the free-text content this
/// app's ticket-creation integrations send — see TicketTemplateDefaults for the fixed catalog of
/// editable Keys.</summary>
[ApiController]
[Route("api/ticket-templates")]
[Authorize]
public class TicketTemplatesController : ControllerBase
{
    private readonly ITicketTemplateService _templates;
    private readonly IAppUserService _appUsers;
    private readonly IAdDirectoryService _ad;

    public TicketTemplatesController(ITicketTemplateService templates, IAppUserService appUsers, IAdDirectoryService ad)
    {
        _templates = templates;
        _appUsers = appUsers;
        _ad = ad;
    }

    private async Task<bool> IsCallerAdminAsync(CancellationToken ct)
    {
        var email = _ad.GetUserInfo(User.GetSamAccountName()).Email;
        return await _appUsers.IsAdminAsync(email, ct);
    }

    [HttpGet]
    public async Task<ActionResult<List<TicketTemplateDto>>> List(CancellationToken ct)
    {
        if (!await IsCallerAdminAsync(ct)) return Forbid();

        var rows = await _templates.ListAsync(ct);
        var byKey = rows.ToDictionary(r => r.Key);

        var result = TicketTemplateDefaults.All.Select(def =>
        {
            byKey.TryGetValue(def.Key, out var row);
            return new TicketTemplateDto
            {
                Key = def.Key,
                Label = def.Label,
                Description = def.Description,
                Content = row?.Content ?? def.DefaultContent,
                DefaultContent = def.DefaultContent,
                Placeholders = def.Placeholders.Select(p => new TicketTemplatePlaceholderDto { Name = p.Name, Description = p.Description }).ToList(),
                UpdatedAt = row?.UpdatedAt,
                UpdatedByDisplayName = row?.UpdatedByDisplayName
            };
        }).ToList();

        return Ok(result);
    }

    [HttpPut("{key}")]
    public async Task<ActionResult<TicketTemplateDto>> Update(string key, UpdateTicketTemplateDto dto, CancellationToken ct)
    {
        if (!await IsCallerAdminAsync(ct)) return Forbid();

        if (!TicketTemplateDefaults.ByKey.TryGetValue(key, out var def))
        {
            return NotFound();
        }
        if (string.IsNullOrWhiteSpace(dto.Content))
        {
            return BadRequest("Le contenu du gabarit ne peut pas être vide.");
        }

        var updatedByDisplayName = _ad.GetUserInfo(User.GetSamAccountName()).DisplayName ?? User.GetObjectId();
        var row = await _templates.UpdateAsync(key, dto.Content, updatedByDisplayName, ct);

        return Ok(new TicketTemplateDto
        {
            Key = def.Key,
            Label = def.Label,
            Description = def.Description,
            Content = row.Content,
            DefaultContent = def.DefaultContent,
            Placeholders = def.Placeholders.Select(p => new TicketTemplatePlaceholderDto { Name = p.Name, Description = p.Description }).ToList(),
            UpdatedAt = row.UpdatedAt,
            UpdatedByDisplayName = row.UpdatedByDisplayName
        });
    }
}
