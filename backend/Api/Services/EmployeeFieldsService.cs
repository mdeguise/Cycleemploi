using Microsoft.EntityFrameworkCore;
using TremblantLifecycle.Api.Data;
using TremblantLifecycle.Api.Models.Entities;

namespace TremblantLifecycle.Api.Services;

public class EmployeeFieldsService : IEmployeeFieldsService
{
    private readonly WorkdayContext _workday;

    public EmployeeFieldsService(WorkdayContext workday)
    {
        _workday = workday;
    }

    public async Task<Dictionary<string, string?>> ResolveAsync(RequestEmployee employee, CancellationToken ct)
    {
        var wd = await _workday.WorkdayDemographics
            .Where(w => w.EmployeeId == employee.WorkdayEmployeeId && w.PrimaryJob == true)
            .FirstOrDefaultAsync(ct);

        // Every distinct job code the employee has ever held (not just the current/primary one) —
        // used by the Freshdesk child ticket group that specifically wants the full history.
        var allJobCodes = await _workday.WorkdayDemographics
            .Where(w => w.EmployeeId == employee.WorkdayEmployeeId && w.JobCode != null && w.JobCode != "")
            .Select(w => w.JobCode!)
            .Distinct()
            .ToListAsync(ct);

        return new Dictionary<string, string?>
        {
            ["EmployeeName"] = employee.NameSnapshot,
            ["EmployeeFirstName"] = wd?.FirstName,
            ["EmployeeLastName"] = wd?.LastName,
            ["EmployeeId"] = employee.WorkdayEmployeeId,
            ["Poste"] = employee.PositionSnapshot ?? wd?.PositionTitle,
            ["PositionCode"] = wd?.Position,
            ["Departement"] = employee.DepartementSnapshot,
            ["Gestionnaire"] = employee.GestionnaireSnapshot ?? wd?.Manager,
            ["TypeEmploi"] = employee.TypeEmploiSnapshot,
            ["CodeEmploi"] = employee.CodeEmploiSnapshot ?? wd?.JobCode,
            ["CostCenter"] = wd?.CostCenter,
            ["HireDate"] = wd?.HireDate?.ToString("yyyy-MM-dd"),
            ["SeniorityDate"] = wd?.SeniorityDate?.ToString("yyyy-MM-dd"),
            ["ActiveStatus"] = wd?.ActiveStatus is null ? null : (wd.ActiveStatus.Value ? "Oui" : "Non"),
            ["LeaveType"] = wd?.LeaveType,
            ["EstimatedLastDayOfLeave"] = wd?.EstimatedLastDayOfLeave,
            ["EmploymentStatus"] = wd?.EmploymentStatus,
            ["PayGroup"] = wd?.PayGroup,
            ["AllJobCodes"] = allJobCodes.Count > 0 ? string.Join(", ", allJobCodes) : null
        };
    }
}
