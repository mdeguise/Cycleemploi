using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TremblantLifecycle.Api.Data;
using TremblantLifecycle.Api.Models.Dtos;
using TremblantLifecycle.Api.Services;

namespace TremblantLifecycle.Api.Controllers;

/// <summary>Manages the D365Approvers table — who gets emailed to fill out a D365 access-approval
/// form, and whether they're global or scoped to a specific Workday Position_Title.
///
/// Two tiers of access, not one: an AppUsers Admin may manage ANYONE's row for ANY title (full
/// control, including AD search — same as before). An existing D365Approver who is NOT an Admin
/// may additionally VIEW this table and add/remove ONLY a row for their OWN account — this is how
/// an approver claims responsibility for a Position_Title themselves, without needing an admin to
/// do it for them. A D365Approver does not need to be an AppUsers Admin, and an AppUsers Admin
/// does not automatically become a D365 approver — those stay separate tables.</summary>
[ApiController]
[Route("api/d365-approvers")]
[Authorize]
public class D365ApproversController : ControllerBase
{
    private readonly ID365ApproverService _approvers;
    private readonly IAppUserService _appUsers;
    private readonly IAdDirectoryService _ad;
    private readonly WorkdayContext _workday;

    public D365ApproversController(ID365ApproverService approvers, IAppUserService appUsers, IAdDirectoryService ad, WorkdayContext workday)
    {
        _approvers = approvers;
        _appUsers = appUsers;
        _ad = ad;
        _workday = workday;
    }

    private Task<bool> IsCallerAdminAsync(CancellationToken ct) =>
        _appUsers.IsAdminAsync(User.GetObjectId(), ct);

    /// <summary>Admin (full control) OR an existing D365Approver (self-service only) — enough to
    /// VIEW the table and the position-title list. Acting is narrower still; see Add/Remove.</summary>
    private async Task<bool> CanViewAsync(CancellationToken ct) =>
        await IsCallerAdminAsync(ct) || await _approvers.HasAnyAccessAsync(User.GetObjectId(), ct);

    private static D365ApproverDto ToDto(Models.Entities.D365Approver a) => new()
    {
        D365ApproverId = a.D365ApproverId,
        Sam = a.Sam,
        DisplayName = a.DisplayName,
        Email = a.Email,
        PositionTitle = a.PositionTitle,
        CreatedAt = a.CreatedAt,
        CreatedByDisplayName = a.CreatedByDisplayName
    };

    [HttpGet]
    public async Task<ActionResult<List<D365ApproverDto>>> List(CancellationToken ct)
    {
        if (!await CanViewAsync(ct)) return Forbid();

        var rows = await _approvers.ListAsync(ct);
        return Ok(rows.Select(ToDto).ToList());
    }

    /// <summary>Same AD people-picker pattern as AppUsersController.AdSearch.</summary>
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

    /// <summary>Every distinct Workday Position_Title currently in use (active/inactive-but-not-
    /// terminated employees, primary job only — same filter as EmployeesController's search) —
    /// the master list the "assign an approver per position title" screen walks through. NOT the
    /// same as D365Approver.PositionTitle values already assigned; a title can appear here with
    /// zero approvers scoped to it, which is exactly what that screen needs to show as a gap.
    /// Each title carries the JobCode(s) currently filed under it, shown for reference only — the
    /// approver-matching itself still keys on PositionTitle alone.</summary>
    [HttpGet("position-titles")]
    public async Task<ActionResult<List<D365PositionTitleDto>>> PositionTitles(CancellationToken ct)
    {
        if (!await CanViewAsync(ct)) return Forbid();

        var rows = await _workday.WorkdayDemographics.AsNoTracking()
            .Where(w => w.PrimaryJob == true && w.EmploymentStatus != "Terminated" && w.PositionTitle != null && w.PositionTitle != "")
            .Select(w => new { w.PositionTitle, w.JobCode })
            .Distinct()
            .ToListAsync(ct);

        var titles = rows
            .GroupBy(r => r.PositionTitle!)
            .Select(g => new D365PositionTitleDto
            {
                PositionTitle = g.Key,
                JobCodes = g.Select(r => r.JobCode)
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Select(c => c!)
                    .Distinct()
                    .OrderBy(c => c)
                    .ToList()
            })
            .OrderBy(t => t.PositionTitle)
            .ToList();

        return Ok(titles);
    }

    [HttpPost]
    public async Task<ActionResult<D365ApproverDto>> Add(CreateD365ApproverDto dto, CancellationToken ct)
    {
        var isAdmin = await IsCallerAdminAsync(ct);
        string sam, displayName;
        string? email;

        if (isAdmin)
        {
            if (string.IsNullOrWhiteSpace(dto.Sam) || string.IsNullOrWhiteSpace(dto.DisplayName))
            {
                return BadRequest("Le compte et le nom sont requis.");
            }
            sam = dto.Sam.Trim();
            displayName = dto.DisplayName.Trim();
            email = dto.Email?.Trim();
        }
        else
        {
            // Self-service: a non-admin D365Approver may only ever add a row for THEMSELVES — the
            // identity is resolved from their own Windows/AD session, never trusted from the
            // request body, so there is no way to claim a title under someone else's name.
            if (!await _approvers.HasAnyAccessAsync(User.GetObjectId(), ct)) return Forbid();

            var callerSam = User.GetSamAccountName();
            var info = _ad.GetUserInfo(callerSam);
            sam = callerSam;
            displayName = info.DisplayName ?? callerSam;
            email = info.Email;
        }

        var addedBy = _ad.GetUserInfo(User.GetSamAccountName()).DisplayName ?? User.GetObjectId();
        var approver = await _approvers.AddAsync(sam, displayName, email, dto.PositionTitle, addedBy, ct);

        return Ok(ToDto(approver));
    }

    [HttpDelete("{d365ApproverId:int}")]
    public async Task<IActionResult> Remove(int d365ApproverId, CancellationToken ct)
    {
        var isAdmin = await IsCallerAdminAsync(ct);
        if (!isAdmin)
        {
            // Self-service mirror of Add(): a non-admin approver may only remove their OWN row.
            var existing = await _approvers.GetAsync(d365ApproverId, ct);
            if (existing is null) return NoContent();

            var callerSam = AppUserService.Normalize(User.GetObjectId());
            if (!string.Equals(existing.Sam, callerSam, StringComparison.OrdinalIgnoreCase)) return Forbid();
        }

        await _approvers.RemoveAsync(d365ApproverId, ct);
        return NoContent();
    }
}
