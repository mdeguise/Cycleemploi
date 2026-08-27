using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TremblantLifecycle.Api.Data;
using TremblantLifecycle.Api.Models.Dtos;
using TremblantLifecycle.Api.Models.Entities;

namespace TremblantLifecycle.Api.Controllers;

/// <summary>Manages the JobCode → D365 security role mappings used when a request selects "Accès
/// D365" — see D365SecurityRoleMapping's doc comment. The Role value must be the exact real D365
/// role name (e.g. "AMC_GL_Preparer"), not an abstracted label — it has to match D365 itself
/// verbatim for the eventual TDX ticket to be meaningful. The catalog of valid roles is therefore
/// sourced dynamically from D365UserSecurityRoles (a real export of current role assignments)
/// rather than a fixed list — see that entity's doc comment.</summary>
[ApiController]
[Route("api/d365-security-roles")]
[Authorize]
public class D365SecurityRolesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly WorkdayContext _workday;

    public D365SecurityRolesController(AppDbContext db, WorkdayContext workday)
    {
        _db = db;
        _workday = workday;
    }

    /// <summary>JobCode -> PositionTitle, from each job code's current primary-job holders — purely
    /// for display in the admin table, so IT can recognize a job code without memorizing it.</summary>
    private async Task<Dictionary<string, string?>> GetPositionTitlesByJobCode(CancellationToken ct)
    {
        return await _workday.WorkdayDemographics
            .Where(w => w.PrimaryJob == true && w.JobCode != null)
            .GroupBy(w => w.JobCode!)
            .Select(g => new { JobCode = g.Key, PositionTitle = g.First().PositionTitle })
            .ToDictionaryAsync(x => x.JobCode, x => x.PositionTitle, ct);
    }

    [HttpGet]
    public async Task<ActionResult<List<D365SecurityRoleMappingDto>>> List(CancellationToken ct)
    {
        var mappings = await _db.D365SecurityRoleMappings
            .OrderBy(m => m.JobCode).ThenBy(m => m.Role)
            .Select(m => new D365SecurityRoleMappingDto { Id = m.Id, JobCode = m.JobCode, Role = m.Role })
            .ToListAsync(ct);

        var titles = await GetPositionTitlesByJobCode(ct);
        foreach (var mapping in mappings)
        {
            titles.TryGetValue(mapping.JobCode, out var title);
            mapping.PositionTitle = title;
        }

        return Ok(mappings);
    }

    /// <summary>Distinct real D365 role names observed in D365UserSecurityRoles, sorted — this is
    /// what the admin page's Role dropdown is built from, so IT can only pick names that are
    /// confirmed to actually exist in D365, no typos/inventions.</summary>
    [HttpGet("catalog")]
    public async Task<ActionResult<List<string>>> Catalog(CancellationToken ct)
    {
        var roles = await _db.D365UserSecurityRoles
            .Select(r => r.SecurityRole)
            .Distinct()
            .OrderBy(r => r)
            .ToListAsync(ct);

        return Ok(roles);
    }

    [HttpPost]
    public async Task<ActionResult<D365SecurityRoleMappingDto>> Create(CreateD365SecurityRoleMappingDto dto, CancellationToken ct)
    {
        var jobCode = dto.JobCode?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(jobCode))
        {
            return BadRequest("JobCode is required.");
        }

        var knownRoles = await _db.D365UserSecurityRoles.Select(r => r.SecurityRole).Distinct().ToListAsync(ct);
        if (!knownRoles.Contains(dto.Role))
        {
            return BadRequest("Role must be a real D365 role name (pick from the dropdown).");
        }

        var alreadyExists = await _db.D365SecurityRoleMappings
            .AnyAsync(m => m.JobCode == jobCode && m.Role == dto.Role, ct);
        if (alreadyExists)
        {
            return Conflict("This job code is already mapped to this role.");
        }

        var mapping = new D365SecurityRoleMapping { JobCode = jobCode, Role = dto.Role };
        _db.D365SecurityRoleMappings.Add(mapping);
        await _db.SaveChangesAsync(ct);

        var titles = await GetPositionTitlesByJobCode(ct);
        titles.TryGetValue(mapping.JobCode, out var positionTitle);

        return Ok(new D365SecurityRoleMappingDto
        {
            Id = mapping.Id,
            JobCode = mapping.JobCode,
            Role = mapping.Role,
            PositionTitle = positionTitle
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var mapping = await _db.D365SecurityRoleMappings.FindAsync([id], ct);
        if (mapping is null) return NotFound();

        _db.D365SecurityRoleMappings.Remove(mapping);
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }
}
