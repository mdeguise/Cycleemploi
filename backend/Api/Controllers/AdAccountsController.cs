using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TremblantLifecycle.Api.Data;
using TremblantLifecycle.Api.Models.Dtos;
using TremblantLifecycle.Api.Services;

namespace TremblantLifecycle.Api.Controllers;

/// <summary>Read-only directory view: every Tremblant employee's AD account, with its
/// Active/Disabled status, for IT to check without opening ADUC. Admin-gated — unlike the "add a
/// user" AD search pickers scattered through the app, this lists the WHOLE roster, not a
/// query-scoped handful of hits.
///
/// Reads PROCESSES.dbo.vw_AdAccount_People (ProcessesContext) rather than a live LDAP query —
/// see ProcessesAdAccount's doc comment. A live query via IAdDirectoryService.GetTremblantAccounts
/// was tried first but only reliably covers iDirectory.itw (the domain vm-trm-live is still
/// joined to); the "T" resort tag this app filters on isn't consistently set on accounts already
/// migrated to ENTERPRISE.AD, so that query silently under-counted. The SQL Agent job behind this
/// view queries ENTERPRISE.AD directly and is the source everything else in this environment
/// already trusts for "does this person have an AD account, and is it enabled".</summary>
[ApiController]
[Route("api/ad-accounts")]
[Authorize]
public class AdAccountsController : ControllerBase
{
    private readonly IAppUserService _appUsers;
    private readonly ProcessesContext _processes;

    public AdAccountsController(IAppUserService appUsers, ProcessesContext processes)
    {
        _appUsers = appUsers;
        _processes = processes;
    }

    [HttpGet]
    public async Task<ActionResult<List<AdAccountStatusDto>>> List(CancellationToken ct)
    {
        if (!await _appUsers.IsAdminAsync(User.GetObjectId(), ct)) return Forbid();

        var accounts = await _processes.AdAccountPeople
            .AsNoTracking()
            .Where(a => a.SamAccountName != null)
            .Select(a => new AdAccountStatusDto
            {
                Sam = a.SamAccountName!,
                DisplayName = a.DisplayName ?? a.SamAccountName!,
                Enabled = a.Enabled ?? false,
                Email = a.Mail,
                EmployeeId = a.EmployeeID
            })
            .OrderBy(a => a.DisplayName)
            .ToListAsync(ct);

        return Ok(accounts);
    }
}
