using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TremblantLifecycle.Api.Models.Dtos;
using TremblantLifecycle.Api.Services;

namespace TremblantLifecycle.Api.Controllers;

/// <summary>Manages the app-managed Ticket Template admin role — see AppUser's doc comment. Only
/// existing admins can view/add/remove other admins (self-referential bootstrap after the initial
/// seeded row).</summary>
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

    private async Task<bool> IsCallerAdminAsync(CancellationToken ct)
    {
        var email = _ad.GetUserInfo(User.GetSamAccountName()).Email;
        return await _appUsers.IsAdminAsync(email, ct);
    }

    [HttpGet]
    public async Task<ActionResult<List<AppUserDto>>> List(CancellationToken ct)
    {
        if (!await IsCallerAdminAsync(ct)) return Forbid();

        var rows = await _appUsers.ListAdminsAsync(ct);
        return Ok(rows.Select(u => new AppUserDto
        {
            AppUserId = u.AppUserId,
            Email = u.Email,
            DisplayName = u.DisplayName,
            CreatedAt = u.CreatedAt,
            CreatedByDisplayName = u.CreatedByDisplayName
        }).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<AppUserDto>> Add(CreateAppUserDto dto, CancellationToken ct)
    {
        if (!await IsCallerAdminAsync(ct)) return Forbid();

        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.DisplayName))
        {
            return BadRequest("Le courriel et le nom sont requis.");
        }

        var addedByDisplayName = _ad.GetUserInfo(User.GetSamAccountName()).DisplayName ?? User.GetObjectId();
        var user = await _appUsers.AddAdminAsync(dto.Email.Trim(), dto.DisplayName.Trim(), addedByDisplayName, ct);

        return Ok(new AppUserDto
        {
            AppUserId = user.AppUserId,
            Email = user.Email,
            DisplayName = user.DisplayName,
            CreatedAt = user.CreatedAt,
            CreatedByDisplayName = user.CreatedByDisplayName
        });
    }

    [HttpDelete("{appUserId:int}")]
    public async Task<IActionResult> Remove(int appUserId, CancellationToken ct)
    {
        if (!await IsCallerAdminAsync(ct)) return Forbid();

        await _appUsers.RemoveAdminAsync(appUserId, ct);
        return NoContent();
    }
}
