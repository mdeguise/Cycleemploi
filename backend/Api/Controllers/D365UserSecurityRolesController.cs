using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TremblantLifecycle.Api.Data;
using TremblantLifecycle.Api.Models.Dtos;

namespace TremblantLifecycle.Api.Controllers;

/// <summary>Correction UI backend for D365UserSecurityRoles — the one-time Excel import left
/// EmployeeId/JobCode/PositionTitle null on rows it couldn't confidently match to a single Workday
/// employee by name (see D365UserSecurityRole's doc comment). This lets an admin search for and
/// link the correct employee, or delete rows that were never going to match (service accounts).</summary>
[ApiController]
[Route("api/d365-user-security-roles")]
[Authorize]
public class D365UserSecurityRolesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly WorkdayContext _workday;

    public D365UserSecurityRolesController(AppDbContext db, WorkdayContext workday)
    {
        _db = db;
        _workday = workday;
    }

    [HttpGet]
    public async Task<ActionResult<List<D365UserSecurityRoleDto>>> List([FromQuery] bool unmatchedOnly, CancellationToken ct)
    {
        var query = _db.D365UserSecurityRoles.AsQueryable();
        if (unmatchedOnly)
        {
            query = query.Where(r => r.EmployeeId == null);
        }

        var rows = await query
            .OrderBy(r => r.UserName).ThenBy(r => r.SecurityRole)
            .Select(r => new D365UserSecurityRoleDto
            {
                Id = r.Id,
                UserName = r.UserName,
                SecurityRole = r.SecurityRole,
                EmployeeId = r.EmployeeId,
                JobCode = r.JobCode,
                PositionTitle = r.PositionTitle
            })
            .ToListAsync(ct);

        return Ok(rows);
    }

    [HttpPut("{id:int}/link")]
    public async Task<ActionResult<D365UserSecurityRoleDto>> Link(int id, LinkD365UserSecurityRoleDto dto, CancellationToken ct)
    {
        var row = await _db.D365UserSecurityRoles.FindAsync([id], ct);
        if (row is null) return NotFound();

        var employee = await _workday.WorkdayDemographics
            .Where(w => w.EmployeeId == dto.EmployeeId && w.PrimaryJob == 1)
            .Select(w => new { w.EmployeeId, w.JobCode, w.PositionTitle })
            .FirstOrDefaultAsync(ct);
        if (employee is null)
        {
            return BadRequest("No active Workday employee found with that EmployeeId.");
        }

        row.EmployeeId = employee.EmployeeId;
        row.JobCode = employee.JobCode;
        row.PositionTitle = employee.PositionTitle;
        await _db.SaveChangesAsync(ct);

        return Ok(new D365UserSecurityRoleDto
        {
            Id = row.Id,
            UserName = row.UserName,
            SecurityRole = row.SecurityRole,
            EmployeeId = row.EmployeeId,
            JobCode = row.JobCode,
            PositionTitle = row.PositionTitle
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var row = await _db.D365UserSecurityRoles.FindAsync([id], ct);
        if (row is null) return NotFound();

        _db.D365UserSecurityRoles.Remove(row);
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }
}
