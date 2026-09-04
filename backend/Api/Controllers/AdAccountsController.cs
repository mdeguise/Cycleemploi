using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TremblantLifecycle.Api.Models.Dtos;
using TremblantLifecycle.Api.Services;

namespace TremblantLifecycle.Api.Controllers;

/// <summary>Read-only directory view: every Tremblant AD account (extensionAttribute2 = "T") with
/// its Active/Disabled status, for IT to check without opening ADUC. Admin-gated — unlike the
/// "add a user" AD search pickers scattered through the app, this lists the WHOLE roster, not a
/// query-scoped handful of hits.</summary>
[ApiController]
[Route("api/ad-accounts")]
[Authorize]
public class AdAccountsController : ControllerBase
{
    private readonly IAppUserService _appUsers;
    private readonly IAdDirectoryService _ad;

    public AdAccountsController(IAppUserService appUsers, IAdDirectoryService ad)
    {
        _appUsers = appUsers;
        _ad = ad;
    }

    [HttpGet]
    public async Task<ActionResult<List<AdAccountStatusDto>>> List(CancellationToken ct)
    {
        if (!await _appUsers.IsAdminAsync(User.GetObjectId(), ct)) return Forbid();

        var accounts = _ad.GetTremblantAccounts()
            .Select(a => new AdAccountStatusDto
            {
                Sam = a.Sam,
                DisplayName = a.Cn ?? a.Sam,
                Enabled = a.Enabled,
                Email = a.Email,
                EmployeeId = a.EmployeeId
            })
            .OrderBy(a => a.DisplayName)
            .ToList();

        return Ok(accounts);
    }
}
