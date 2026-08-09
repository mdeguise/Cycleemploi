using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TremblantLifecycle.Api.Data;
using TremblantLifecycle.Api.Models.Dtos;
using TremblantLifecycle.Api.Models.Entities;

namespace TremblantLifecycle.Api.Controllers;

/// <summary>Manages the per-job-code "answers" to the TDX "D365 - Access" form (FormID 10799) — see
/// D365JobCodeTemplate's doc comment. One template per job code; a template's existence is what
/// "form filled out" means. This is the automation target: once every in-use job code has a
/// template, submitting a D365 access request for a new hire can look up their job code here and
/// build the TDX ticket payload directly, no manual form-filling.</summary>
[ApiController]
[Route("api/d365-jobcode-templates")]
[Authorize]
public class D365JobCodeTemplatesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly WorkdayContext _workday;

    public D365JobCodeTemplatesController(AppDbContext db, WorkdayContext workday)
    {
        _db = db;
        _workday = workday;
    }

    private async Task<Dictionary<string, string?>> GetPositionTitlesByJobCode(CancellationToken ct)
    {
        return await _workday.WorkdayDemographics
            .Where(w => w.PrimaryJob == 1 && w.JobCode != null)
            .GroupBy(w => w.JobCode!)
            .Select(g => new { JobCode = g.Key, PositionTitle = g.First().PositionTitle })
            .ToDictionaryAsync(x => x.JobCode, x => x.PositionTitle, ct);
    }

    /// <summary>Every job code currently held by an active primary-job employee, each flagged with
    /// whether its D365 access template has been filled out yet — the progress tracker for the
    /// automation rollout.</summary>
    [HttpGet]
    public async Task<ActionResult<List<D365JobCodeTemplateSummaryDto>>> List(CancellationToken ct)
    {
        var titles = await GetPositionTitlesByJobCode(ct);
        var filledJobCodes = await _db.D365JobCodeTemplates.Select(t => t.JobCode).ToListAsync(ct);
        var filledSet = filledJobCodes.ToHashSet();

        var result = titles
            .Select(kv => new D365JobCodeTemplateSummaryDto
            {
                JobCode = kv.Key,
                PositionTitle = kv.Value,
                IsFilled = filledSet.Contains(kv.Key)
            })
            .OrderBy(x => x.JobCode)
            .ToList();

        return Ok(result);
    }

    /// <summary>The fixed set of role checkboxes on the real TDX form — see
    /// TdxD365RoleCheckboxes.All.</summary>
    [HttpGet("catalog")]
    public ActionResult<List<string>> Catalog()
    {
        return Ok(TdxD365RoleCheckboxes.All);
    }

    [HttpGet("{jobCode}")]
    public async Task<ActionResult<D365JobCodeTemplateDto>> Get(string jobCode, CancellationToken ct)
    {
        var template = await _db.D365JobCodeTemplates
            .Include(t => t.Roles)
            .FirstOrDefaultAsync(t => t.JobCode == jobCode, ct);

        var titles = await GetPositionTitlesByJobCode(ct);
        titles.TryGetValue(jobCode, out var positionTitle);

        if (template is null)
        {
            return Ok(new D365JobCodeTemplateDto { JobCode = jobCode, PositionTitle = positionTitle, IsFilled = false });
        }

        return Ok(new D365JobCodeTemplateDto
        {
            JobCode = template.JobCode,
            PositionTitle = positionTitle,
            LegalEntity = template.LegalEntity,
            DepartmentNumber = template.DepartmentNumber,
            ApprovalLimit = template.ApprovalLimit,
            ApAccessDetails = template.ApAccessDetails,
            AdditionalLegalEntities = template.AdditionalLegalEntities,
            Roles = template.Roles.Select(r => r.Role).OrderBy(r => r).ToList(),
            IsFilled = true
        });
    }

    [HttpPut("{jobCode}")]
    public async Task<ActionResult<D365JobCodeTemplateDto>> Upsert(string jobCode, UpsertD365JobCodeTemplateDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.LegalEntity))
        {
            return BadRequest("Legal Entity is required.");
        }
        if (string.IsNullOrWhiteSpace(dto.DepartmentNumber))
        {
            return BadRequest("Department Number is required.");
        }
        if (dto.ApprovalLimit < 0)
        {
            return BadRequest("Approval Limit cannot be negative.");
        }
        var invalidRoles = dto.Roles.Except(TdxD365RoleCheckboxes.All).ToList();
        if (invalidRoles.Count > 0)
        {
            return BadRequest($"Unknown role(s): {string.Join(", ", invalidRoles)}");
        }

        var template = await _db.D365JobCodeTemplates
            .Include(t => t.Roles)
            .FirstOrDefaultAsync(t => t.JobCode == jobCode, ct);

        if (template is null)
        {
            template = new D365JobCodeTemplate { JobCode = jobCode };
            _db.D365JobCodeTemplates.Add(template);
        }

        template.LegalEntity = dto.LegalEntity.Trim();
        template.DepartmentNumber = dto.DepartmentNumber.Trim();
        template.ApprovalLimit = dto.ApprovalLimit;
        template.ApAccessDetails = string.IsNullOrWhiteSpace(dto.ApAccessDetails) ? null : dto.ApAccessDetails.Trim();
        template.AdditionalLegalEntities = string.IsNullOrWhiteSpace(dto.AdditionalLegalEntities) ? null : dto.AdditionalLegalEntities.Trim();

        template.Roles.Clear();
        foreach (var role in dto.Roles.Distinct())
        {
            template.Roles.Add(new D365JobCodeTemplateRole { Role = role });
        }

        await _db.SaveChangesAsync(ct);

        var titles = await GetPositionTitlesByJobCode(ct);
        titles.TryGetValue(jobCode, out var positionTitle);

        return Ok(new D365JobCodeTemplateDto
        {
            JobCode = template.JobCode,
            PositionTitle = positionTitle,
            LegalEntity = template.LegalEntity,
            DepartmentNumber = template.DepartmentNumber,
            ApprovalLimit = template.ApprovalLimit,
            ApAccessDetails = template.ApAccessDetails,
            AdditionalLegalEntities = template.AdditionalLegalEntities,
            Roles = template.Roles.Select(r => r.Role).OrderBy(r => r).ToList(),
            IsFilled = true
        });
    }

    [HttpDelete("{jobCode}")]
    public async Task<IActionResult> Delete(string jobCode, CancellationToken ct)
    {
        var template = await _db.D365JobCodeTemplates.FirstOrDefaultAsync(t => t.JobCode == jobCode, ct);
        if (template is null) return NotFound();

        _db.D365JobCodeTemplates.Remove(template);
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }
}
