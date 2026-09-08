using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TremblantLifecycle.Api.Data;
using TremblantLifecycle.Api.Models.Dtos;
using TremblantLifecycle.Api.Services;

namespace TremblantLifecycle.Api.Controllers;

/// <summary>Read-only directory view: every Tremblant employee's AD account, with its
/// Active/Disabled status, for IT to check without opening ADUC. Admin OR any D365Approver may
/// view it (a D365Approver routinely needs to confirm a requesting employee's account is even
/// active before approving) — unlike the "add a user" AD search pickers scattered through the
/// app, this lists the WHOLE roster, not a query-scoped handful of hits.
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
    private readonly ID365ApproverService _d365Approvers;
    private readonly ProcessesContext _processes;
    private readonly IAdDirectoryService _ad;
    private readonly ITdxService _tdx;

    public AdAccountsController(IAppUserService appUsers, ID365ApproverService d365Approvers, ProcessesContext processes, IAdDirectoryService ad, ITdxService tdx)
    {
        _appUsers = appUsers;
        _d365Approvers = d365Approvers;
        _processes = processes;
        _ad = ad;
        _tdx = tdx;
    }

    /// <summary>Admin (full control) OR any D365Approver (global or scoped) — same "who can view"
    /// pattern as D365ApproversController.CanViewAsync.</summary>
    private async Task<bool> CanViewAsync(CancellationToken ct)
    {
        var objectId = User.GetObjectId();
        return await _appUsers.IsAdminAsync(objectId, ct) || await _d365Approvers.HasAnyAccessAsync(objectId, ct);
    }

    [HttpGet]
    public async Task<ActionResult<List<AdAccountStatusDto>>> List(CancellationToken ct)
    {
        if (!await CanViewAsync(ct)) return Forbid();

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

    /// <summary>Creates a real TDX ticket asking IT Operations to re-enable the given account —
    /// a direct user action awaiting a result, so failures are surfaced to the caller rather than
    /// swallowed/emailed, same pattern as AuthController.CreateHelpTicket.</summary>
    [HttpPost("{sam}/reactivate-ticket")]
    public async Task<ActionResult<ReactivateAccountTicketResultDto>> CreateReactivationTicket(string sam, [FromBody] ReactivateAccountTicketDto dto, CancellationToken ct)
    {
        if (!await CanViewAsync(ct)) return Forbid();
        if (string.IsNullOrWhiteSpace(sam)) return BadRequest("Le compte est requis.");

        var requesterSam = User.GetSamAccountName();
        var requesterInfo = _ad.GetUserInfo(requesterSam);
        if (string.IsNullOrWhiteSpace(requesterInfo.Email))
        {
            return Problem("Impossible de déterminer votre adresse courriel.", statusCode: StatusCodes.Status500InternalServerError);
        }

        try
        {
            var ticketId = await _tdx.CreateAccountReactivationTicketAsync(
                sam,
                string.IsNullOrWhiteSpace(dto.DisplayName) ? sam : dto.DisplayName,
                requesterInfo.DisplayName ?? requesterSam,
                requesterInfo.Email,
                ct);
            return Ok(new ReactivateAccountTicketResultDto { TicketId = ticketId });
        }
        catch (TdxTicketException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
        }
    }
}
