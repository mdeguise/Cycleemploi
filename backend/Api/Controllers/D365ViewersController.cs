using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TremblantLifecycle.Api.Models.Dtos;
using TremblantLifecycle.Api.Services;

namespace TremblantLifecycle.Api.Controllers;

/// <summary>Manages the D365Viewers table — "IT Personnel" who may see the D365 access-approval
/// tracking list and every request's status, but never fill out or send the approval form. Gated
/// on the same AppUsers Admin role as D365ApproversController, for the same reason: managing WHO
/// can view is an administrative action, not something a viewer grants themselves.</summary>
[ApiController]
[Route("api/d365-viewers")]
[Authorize]
public class D365ViewersController : ControllerBase
{
    private readonly ID365ViewerService _viewers;
    private readonly IAppUserService _appUsers;
    private readonly IAdDirectoryService _ad;

    public D365ViewersController(ID365ViewerService viewers, IAppUserService appUsers, IAdDirectoryService ad)
    {
        _viewers = viewers;
        _appUsers = appUsers;
        _ad = ad;
    }

    private Task<bool> IsCallerAdminAsync(CancellationToken ct) =>
        _appUsers.IsAdminAsync(User.GetObjectId(), ct);

    private static D365ViewerDto ToDto(Models.Entities.D365Viewer v) => new()
    {
        D365ViewerId = v.D365ViewerId,
        Sam = v.Sam,
        DisplayName = v.DisplayName,
        Email = v.Email,
        CreatedAt = v.CreatedAt,
        CreatedByDisplayName = v.CreatedByDisplayName
    };

    [HttpGet]
    public async Task<ActionResult<List<D365ViewerDto>>> List(CancellationToken ct)
    {
        if (!await IsCallerAdminAsync(ct)) return Forbid();

        var rows = await _viewers.ListAsync(ct);
        return Ok(rows.Select(ToDto).ToList());
    }

    /// <summary>Same AD people-picker pattern as D365ApproversController.AdSearch.</summary>
    [HttpGet("ad-search")]
    public async Task<ActionResult<List<AdAccountDto>>> AdSearch([FromQuery] string q, CancellationToken ct)
    {
        if (!await IsCallerAdminAsync(ct)) return Forbid();
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2) return Ok(new List<AdAccountDto>());

        var hits = _ad.SearchAccounts(q.Trim(), 15);
        return Ok(hits.Select(a => new AdAccountDto
        {
            Sam = a.Sam,
            DisplayName = a.Cn ?? a.Sam,
            Email = a.Email
        }).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<D365ViewerDto>> Add(CreateD365ViewerDto dto, CancellationToken ct)
    {
        if (!await IsCallerAdminAsync(ct)) return Forbid();

        if (string.IsNullOrWhiteSpace(dto.Sam) || string.IsNullOrWhiteSpace(dto.DisplayName))
        {
            return BadRequest("Le compte et le nom sont requis.");
        }

        var addedBy = _ad.GetUserInfo(User.GetSamAccountName()).DisplayName ?? User.GetObjectId();
        var viewer = await _viewers.AddAsync(dto.Sam.Trim(), dto.DisplayName.Trim(), dto.Email?.Trim(), addedBy, ct);

        return Ok(ToDto(viewer));
    }

    [HttpDelete("{d365ViewerId:int}")]
    public async Task<IActionResult> Remove(int d365ViewerId, CancellationToken ct)
    {
        if (!await IsCallerAdminAsync(ct)) return Forbid();

        await _viewers.RemoveAsync(d365ViewerId, ct);
        return NoContent();
    }
}
