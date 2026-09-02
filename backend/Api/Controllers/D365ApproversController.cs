using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TremblantLifecycle.Api.Data;
using TremblantLifecycle.Api.Models.Dtos;
using TremblantLifecycle.Api.Services;

namespace TremblantLifecycle.Api.Controllers;

/// <summary>Manages the D365Approvers table — who gets emailed to fill out a D365 access-approval
/// form, and whether they're global or scoped to a specific Workday Position_Title. Gated on the
/// existing AppUsers Admin role (same people who manage the ticket templates / other Administration
/// users), NOT self-referential the way AppUsers is — a D365 approver does not need to be an
/// AppUsers Admin, and an AppUsers Admin does not automatically become a D365 approver.</summary>
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
        if (!await IsCallerAdminAsync(ct)) return Forbid();

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
    /// zero approvers scoped to it, which is exactly what that screen needs to show as a gap.</summary>
    [HttpGet("position-titles")]
    public async Task<ActionResult<List<string>>> PositionTitles(CancellationToken ct)
    {
        if (!await IsCallerAdminAsync(ct)) return Forbid();

        var titles = await _workday.WorkdayDemographics.AsNoTracking()
            .Where(w => w.PrimaryJob == true && w.EmploymentStatus != "Terminated" && w.PositionTitle != null && w.PositionTitle != "")
            .Select(w => w.PositionTitle!)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync(ct);

        return Ok(titles);
    }

    [HttpPost]
    public async Task<ActionResult<D365ApproverDto>> Add(CreateD365ApproverDto dto, CancellationToken ct)
    {
        if (!await IsCallerAdminAsync(ct)) return Forbid();

        if (string.IsNullOrWhiteSpace(dto.Sam) || string.IsNullOrWhiteSpace(dto.DisplayName))
        {
            return BadRequest("Le compte et le nom sont requis.");
        }

        var addedBy = _ad.GetUserInfo(User.GetSamAccountName()).DisplayName ?? User.GetObjectId();
        var approver = await _approvers.AddAsync(dto.Sam.Trim(), dto.DisplayName.Trim(), dto.Email?.Trim(), dto.PositionTitle, addedBy, ct);

        return Ok(ToDto(approver));
    }

    [HttpDelete("{d365ApproverId:int}")]
    public async Task<IActionResult> Remove(int d365ApproverId, CancellationToken ct)
    {
        if (!await IsCallerAdminAsync(ct)) return Forbid();

        await _approvers.RemoveAsync(d365ApproverId, ct);
        return NoContent();
    }
}
