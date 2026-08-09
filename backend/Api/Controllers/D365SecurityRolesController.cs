using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TremblantLifecycle.Api.Data;
using TremblantLifecycle.Api.Models.Dtos;
using TremblantLifecycle.Api.Models.Entities;

namespace TremblantLifecycle.Api.Controllers;

/// <summary>Manages the JobCode → D365 security role mappings used when a request selects "Accès
/// D365" — see D365SecurityRoleMapping's doc comment. Starts empty; populated by hand via this
/// admin API as IT figures out which job codes need which roles.</summary>
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
            .Where(w => w.PrimaryJob == 1 && w.JobCode != null)
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

    /// <summary>The fixed set of valid role values — the frontend also hardcodes this list (see
    /// src/data/catalogs.ts's D365_SECURITY_ROLES), this endpoint exists so the two can never drift
    /// silently; the frontend could switch to fetching this instead of duplicating it if that
    /// becomes worth doing.</summary>
    [HttpGet("catalog")]
    public ActionResult<List<string>> Catalog()
    {
        return Ok(D365SecurityRoles.All);
    }

    [HttpPost]
    public async Task<ActionResult<D365SecurityRoleMappingDto>> Create(CreateD365SecurityRoleMappingDto dto, CancellationToken ct)
    {
        var jobCode = dto.JobCode?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(jobCode))
        {
            return BadRequest("JobCode is required.");
        }

        if (!D365SecurityRoles.All.Contains(dto.Role))
        {
            return BadRequest($"Role must be one of: {string.Join(", ", D365SecurityRoles.All)}");
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
