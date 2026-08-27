using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TremblantLifecycle.Api.Models.Dtos;
using TremblantLifecycle.Api.Models.Entities;
using TremblantLifecycle.Api.Services;

namespace TremblantLifecycle.Api.Controllers;

/// <summary>Lets a Ticket Template admin (see AppUser) view and edit the structured content this
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

    // Keyed on the Windows identity, not the AD email — admin (*_adm) accounts have no `mail`
    // attribute, which used to deny them silently.
    private Task<bool> IsCallerAdminAsync(CancellationToken ct) =>
        _appUsers.IsAdminAsync(User.GetObjectId(), ct);

    private static TicketTemplateDto ToDto(TicketTemplateDefinition def, TicketTemplate? row)
    {
        return new TicketTemplateDto
        {
            Key = def.Key,
            Label = def.Label,
            Description = def.Description,
            Shape = def.Shape.ToString(),
            Content = row?.Content ?? def.DefaultContent,
            DefaultContent = def.DefaultContent,
            RequestFields = def.RequestFields.Select(f => new TicketTemplateFieldDto { Key = f.Key, Label = f.Label }).ToList(),
            EmployeeFields = def.AllowsEmployeeFields
                ? TicketTemplateFieldCatalog.EmployeeFields.Select(f => new TicketTemplateFieldDto { Key = f.Key, Label = f.Label }).ToList()
                : [],
            UpdatedAt = row?.UpdatedAt,
            UpdatedByDisplayName = row?.UpdatedByDisplayName
        };
    }

    [HttpGet]
    public async Task<ActionResult<List<TicketTemplateDto>>> List(CancellationToken ct)
    {
        if (!await IsCallerAdminAsync(ct)) return Forbid();

        var rows = await _templates.ListAsync(ct);
        var byKey = rows.ToDictionary(r => r.Key);

        var result = TicketTemplateDefaults.All
            .Select(def => ToDto(def, byKey.GetValueOrDefault(def.Key)))
            .ToList();

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
        if (!IsValidForShape(dto.Content, def.Shape))
        {
            return BadRequest("Le contenu du gabarit n'est pas valide pour ce type de billet.");
        }

        var updatedByDisplayName = _ad.GetUserInfo(User.GetSamAccountName()).DisplayName ?? User.GetObjectId();
        var row = await _templates.UpdateAsync(key, dto.Content, updatedByDisplayName, ct);

        return Ok(ToDto(def, row));
    }

    private static bool IsValidForShape(string content, TicketTemplateShape shape)
    {
        try
        {
            if (shape == TicketTemplateShape.Inline)
            {
                var parsed = JsonSerializer.Deserialize<InlineTemplateContent>(content, JsonOptions);
                return parsed is not null;
            }
            else
            {
                var parsed = JsonSerializer.Deserialize<BlockTemplateContent>(content, JsonOptions);
                return parsed is not null;
            }
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
}
