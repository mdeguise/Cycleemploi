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

    /// <summary>Searches WorkdayDemographic by number/last name/first name, filtered to active
    /// employees (Employment_Status != "Terminated" — NOT == "Active", since "Inactive" covers
    /// on-leave/layoff, which the business treats as active for this app — see the
    /// WorkdayDemographic entity's doc comment) and collapsed to one row per employee via
    /// PrimaryJob == true. TODO: confirm the "!= Terminated" business rule with HR/the business owner
    /// before launch — it's what the data and the app's own "seuls les employés actifs" notice
    /// text both point to, but wasn't explicitly re-confirmed after the schema discovery.</summary>
    [HttpGet("search")]
    public async Task<ActionResult<List<EmployeeDto>>> Search([FromQuery] string q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
        {
            return Ok(new List<EmployeeDto>());
        }

        var terms = q.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var query = _workday.WorkdayDemographics
            .Where(e => e.PrimaryJob == true && e.EmploymentStatus != "Terminated");

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
