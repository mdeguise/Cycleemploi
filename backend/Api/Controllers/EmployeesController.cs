using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TremblantLifecycle.Api.Data;
using TremblantLifecycle.Api.Models.Dtos;

namespace TremblantLifecycle.Api.Controllers;

[ApiController]
[Route("api/employees")]
[Authorize]
public class EmployeesController : ControllerBase
{
    private readonly WorkdayContext _workday;

    public EmployeesController(WorkdayContext workday)
    {
        _workday = workday;
    }

    /// <summary>Searches WorkdayDemographic by number/last name/first name, collapsed to one row per
    /// employee via PrimaryJob == true. Filtered to active employees (Employment_Status !=
    /// "Terminated" — NOT == "Active", since "Inactive" covers on-leave/layoff, which the business
    /// treats as active for this app — see the WorkdayDemographic entity's doc comment) UNLESS
    /// includeTerminated is set — Réactivation's entire purpose is finding someone who left, so the
    /// wizard passes includeTerminated=true there specifically (see Step1Employee.tsx); every other
    /// caller (Onboarding, Offboarding, the D365 ad-hoc apps) still only searches active people.</summary>
    [HttpGet("search")]
    public async Task<ActionResult<List<EmployeeDto>>> Search([FromQuery] string q, [FromQuery] bool includeTerminated, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
        {
            return Ok(new List<EmployeeDto>());
        }

        var terms = q.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var query = _workday.WorkdayDemographics
            .Where(e => e.PrimaryJob == true && (includeTerminated || e.EmploymentStatus != "Terminated"));

        if (terms.Length <= 1)
        {
            query = query.Where(e =>
                EF.Functions.Like(e.EmployeeId, $"{q}%") ||
                EF.Functions.Like(e.LastName!, $"{q}%") ||
                EF.Functions.Like(e.FirstName!, $"{q}%"));
        }
        else
        {
            // Multi-word query (e.g. "marc de") — every word must match the start of either the
            // first or last name, in any order, since we don't know which word the user typed as
            // the first vs. last name.
            foreach (var term in terms)
            {
                query = query.Where(e =>
                    EF.Functions.Like(e.FirstName!, $"{term}%") ||
                    EF.Functions.Like(e.LastName!, $"{term}%"));
            }
        }

        var results = await query
            .OrderBy(e => e.LastName)
            .Take(6)
            .Select(e => new EmployeeDto
            {
                EmployeeId = e.EmployeeId,
                Prenom = e.PreferredFirstName != null && e.PreferredFirstName != "" ? e.PreferredFirstName : e.FirstName ?? "",
                Nom = e.LastName ?? "",
                Poste = e.PositionTitle,
                Departement = e.JobFamilyGroup,
                CodeEmploi = e.JobCode,
                TypeEmploi = e.TimeType != null && e.WorkerType != null ? $"{e.TimeType} — {e.WorkerType}" : e.TimeType,
                Gestionnaire = e.Manager,
                PayGroup = e.PayGroup
            })
            .ToListAsync(ct);

        return Ok(results);
    }

    /// <summary>Every position title currently held by a non-terminated employee (with its job code and
    /// department), for the Réactivation "poste de retour" picker. One entry per title.</summary>
    [HttpGet("positions")]
    public async Task<ActionResult<List<PositionOptionDto>>> Positions(CancellationToken ct)
    {
        var rows = await _workday.WorkdayDemographics
            .Where(e => e.PrimaryJob == true && e.EmploymentStatus != "Terminated" && e.PositionTitle != null && e.PositionTitle != "")
            .Select(e => new { e.PositionTitle, e.JobCode, e.JobFamilyGroup })
            .ToListAsync(ct);

        var list = rows
            .GroupBy(r => r.PositionTitle!)
            .Select(g => g.GroupBy(x => x.JobCode).OrderByDescending(x => x.Count()).First())
            .Select(g => new PositionOptionDto { Poste = g.First().PositionTitle!, CodeEmploi = g.Key, Departement = g.First().JobFamilyGroup })
            .OrderBy(p => p.Poste)
            .ToList();
        return Ok(list);
    }

    [HttpGet("{workdayId}")]
    public async Task<ActionResult<EmployeeDto>> GetById(string workdayId, CancellationToken ct)
    {
        var employee = await _workday.WorkdayDemographics
            .Where(e => e.EmployeeId == workdayId && e.PrimaryJob == true)
            .Select(e => new EmployeeDto
            {
                EmployeeId = e.EmployeeId,
                Prenom = e.PreferredFirstName != null && e.PreferredFirstName != "" ? e.PreferredFirstName : e.FirstName ?? "",
                Nom = e.LastName ?? "",
                Poste = e.PositionTitle,
                Departement = e.JobFamilyGroup,
                CodeEmploi = e.JobCode,
                TypeEmploi = e.TimeType != null && e.WorkerType != null ? $"{e.TimeType} — {e.WorkerType}" : e.TimeType,
                Gestionnaire = e.Manager,
                PayGroup = e.PayGroup
            })
            .FirstOrDefaultAsync(ct);

        return employee is null ? NotFound() : Ok(employee);
    }
}

public class PositionOptionDto
{
    public string Poste { get; set; } = "";
    public string? CodeEmploi { get; set; }
    public string? Departement { get; set; }
}
