using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TremblantLifecycle.Api.Models.Dtos;
using TremblantLifecycle.Api.Models.Entities;
using TremblantLifecycle.Api.Services;

namespace TremblantLifecycle.Api.Controllers;

/// <summary>Manages app-managed access to the Administration section — see <see cref="AppUser"/>.
/// Only Admins can view/add/remove/re-role users (self-referential after the initial seeded row, with
/// Access:BootstrapAdmins as the recovery path). Everything here keys on the caller's Windows
/// identity, never on their AD email.</summary>
[ApiController]
[Route("api/app-users")]
[Authorize]
public class AppUsersController : ControllerBase
{
    private readonly IAppUserService _appUsers;
    private readonly IAdDirectoryService _ad;

    public AppUsersController(IAppUserService appUsers, IAdDirectoryService ad)
    {
        _appUsers = appUsers;
        _ad = ad;
    }

    private Task<bool> IsCallerAdminAsync(CancellationToken ct) =>
        _appUsers.IsAdminAsync(User.GetObjectId(), ct);

    private static AppUserRole ParseRole(string? role) =>
        Enum.TryParse<AppUserRole>(role, ignoreCase: true, out var parsed) ? parsed : AppUserRole.Lecteur;

    private static AppUserDto ToDto(AppUser u) => new()
    {
        AppUserId = u.AppUserId,
        Sam = u.Sam,
        DisplayName = u.DisplayName,
        Email = u.Email,
        Role = u.Role.ToString(),
        CreatedAt = u.CreatedAt,
        CreatedByDisplayName = u.CreatedByDisplayName
    };

    [HttpGet]
    public async Task<ActionResult<List<AppUserDto>>> List(CancellationToken ct)
    {
        if (!await IsCallerAdminAsync(ct)) return Forbid();

        var rows = await _appUsers.ListAsync(ct);
        return Ok(rows.Select(ToDto).ToList());
    }

    /// <summary>Searches AD so an admin picks a real account instead of typing a sAMAccountName by
    /// hand — a typo there would create a row that can never match anyone.</summary>
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
    public async Task<ActionResult<AppUserDto>> Add(CreateAppUserDto dto, CancellationToken ct)
    {
        if (!await IsCallerAdminAsync(ct)) return Forbid();

        if (string.IsNullOrWhiteSpace(dto.Sam) || string.IsNullOrWhiteSpace(dto.DisplayName))
        {
            return BadRequest("Le compte et le nom sont requis.");
        }

        var addedBy = _ad.GetUserInfo(User.GetSamAccountName()).DisplayName ?? User.GetObjectId();
        var user = await _appUsers.AddAsync(dto.Sam.Trim(), dto.DisplayName.Trim(), dto.Email?.Trim(), ParseRole(dto.Role), addedBy, ct);

        return Ok(ToDto(user));
    }

    [HttpPut("{appUserId:int}/role")]
    public async Task<ActionResult<AppUserDto>> UpdateRole(int appUserId, UpdateAppUserRoleDto dto, CancellationToken ct)
    {
        if (!await IsCallerAdminAsync(ct)) return Forbid();

        var updated = await _appUsers.UpdateRoleAsync(appUserId, ParseRole(dto.Role), ct);
        if (updated is null)
        {
            return Conflict("Impossible de modifier ce rôle : il doit rester au moins un administrateur.");
        }
        return Ok(ToDto(updated));
    }

    [HttpDelete("{appUserId:int}")]
    public async Task<IActionResult> Remove(int appUserId, CancellationToken ct)
    {
        if (!await IsCallerAdminAsync(ct)) return Forbid();

        var removed = await _appUsers.RemoveAsync(appUserId, ct);
        if (!removed)
        {
            return Conflict("Le dernier administrateur ne peut pas être retiré.");
        }
        return NoContent();
    }
}
