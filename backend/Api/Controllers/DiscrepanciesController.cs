using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TremblantLifecycle.Api.Data;
using TremblantLifecycle.Api.Models.Dtos;
using TremblantLifecycle.Api.Services;

namespace TremblantLifecycle.Api.Controllers;

/// <summary>Read-only reconciliation ("Écarts") view for HR/IT: cross-references the imported D365
/// security-role users, the imported Dynaway licenses, live Active Directory (Tremblant accounts),
/// and the read-only Workday demographic table to surface four kinds of discrepancy. HR-admin only
/// (TRM-RH-ADM) — it exposes account-status data across the whole Tremblant population.</summary>
[ApiController]
[Route("api/discrepancies")]
[Authorize]
public class DiscrepanciesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly WorkdayContext _workday;
    private readonly IAdDirectoryService _ad;
    private readonly string _hrGroup;

    public DiscrepanciesController(AppDbContext db, WorkdayContext workday, IAdDirectoryService ad,
        IOptions<HrGroupOptions> hr)
    {
        _db = db;
        _workday = workday;
        _ad = ad;
        _hrGroup = hr.Value.TrmRhAdmGroupName;
    }

    [HttpGet]
    public async Task<ActionResult<DiscrepanciesDto>> Get(CancellationToken ct)
    {
        var caller = User.Identity?.Name ?? "";
        if (!_ad.IsUserInGroup(caller, _hrGroup)) return Forbid();

        var d365 = await _db.D365UserSecurityRoles.AsNoTracking().ToListAsync(ct);
        var dyn = await _db.DynawayUsers.AsNoTracking().ToListAsync(ct);
        var ad = _ad.GetTremblantAccounts();

        var adBySam = ad.Where(a => !string.IsNullOrEmpty(a.Sam))
            .GroupBy(a => a.Sam.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First());
        var adByCn = ad.Where(a => !string.IsNullOrEmpty(a.Cn))
            .GroupBy(a => Norm(a.Cn!))
            .ToDictionary(g => g.Key, g => g.First());

        // Distinct D365 users (the source is one row per user+role).
        var d365Users = d365
            .GroupBy(r => r.UserName)
            .Select(g => new D365User(
                g.Key,
                g.Select(x => x.EmployeeId).FirstOrDefault(x => !string.IsNullOrEmpty(x)),
                g.Select(x => x.SecurityRole).Distinct().OrderBy(x => x).ToList()))
            .ToList();
        var d365ByNorm = d365Users.ToDictionary(u => Norm(u.UserName), u => u, StringComparer.Ordinal);

        // Workday employment status for the D365 users we could link to an EmployeeId.
        var empIds = d365Users.Where(u => !string.IsNullOrEmpty(u.EmployeeId))
            .Select(u => u.EmployeeId!).Distinct().ToList();
        var wdByEmp = (await _workday.WorkdayDemographics
                .Where(w => w.PrimaryJob == 1 && empIds.Contains(w.EmployeeId))
                .Select(w => new { w.EmployeeId, w.EmploymentStatus })
                .ToListAsync(ct))
            .GroupBy(w => w.EmployeeId)
            .ToDictionary(g => g.Key, g => g.First().EmploymentStatus);

        // ---- #1 Tremblant Dynaway (login resolves to a Tremblant AD account, ext2=T) ----
        var tremDyn = dyn
            .Where(d => !string.IsNullOrEmpty(d.Login) && adBySam.ContainsKey(d.Login!.ToLowerInvariant()))
            .Select(d => (Dyn: d, Ad: adBySam[d.Login!.ToLowerInvariant()]))
            .ToList();

        var tremblantDynaway = tremDyn.Select(t =>
        {
            var cnNorm = t.Ad.Cn is null ? "" : Norm(t.Ad.Cn);
            var hasRole = cnNorm.Length > 0 && d365ByNorm.TryGetValue(cnNorm, out var u);
            var roleCount = hasRole ? d365ByNorm[cnNorm].Roles.Count : 0;
            return new TremblantDynawayRowDto
            {
                Name = t.Ad.Cn ?? t.Dyn.Name,
                Login = t.Dyn.Login,
                AdEnabled = t.Ad.Enabled,
                HasD365Role = hasRole,
                D365RoleCount = roleCount,
            };
        }).OrderBy(r => r.Name).ToList();

        // ---- #2a No active AD account ----
        var noAd = new List<NoActiveAdRowDto>();
        foreach (var t in tremDyn)
            if (!t.Ad.Enabled)
                noAd.Add(new NoActiveAdRowDto { Source = "Dynaway (T)", Name = t.Ad.Cn ?? t.Dyn.Name ?? "", Login = t.Dyn.Login, Status = "Disabled" });
        foreach (var u in d365Users)
        {
            if (adByCn.TryGetValue(Norm(u.UserName), out var a))
            {
                if (!a.Enabled)
                    noAd.Add(new NoActiveAdRowDto { Source = "D365", Name = u.UserName, Login = a.Sam, Status = "Disabled" });
            }
            else
            {
                noAd.Add(new NoActiveAdRowDto { Source = "D365", Name = u.UserName, Login = null, Status = "No AD account" });
            }
        }
        noAd = noAd.OrderBy(r => r.Source).ThenBy(r => r.Name).ToList();

        // ---- #2b Tremblant Dynaway with no D365 role ----
        var dynawayNoRole = tremblantDynaway
            .Where(r => !r.HasD365Role)
            .Select(r => new DynawayNoRoleRowDto { Name = r.Name, Login = r.Login, AdEnabled = r.AdEnabled })
            .ToList();

        // ---- #2c D365 users whose Workday demographic is not Active ----
        var inactive = new List<D365InactiveWorkdayRowDto>();
        foreach (var u in d365Users)
        {
            string status;
            if (string.IsNullOrEmpty(u.EmployeeId))
                status = "Not linked";
            else if (!wdByEmp.TryGetValue(u.EmployeeId!, out var st) || string.IsNullOrEmpty(st))
                status = "No Workday record";
            else if (string.Equals(st, "Active", StringComparison.OrdinalIgnoreCase))
                continue; // Active -> not a discrepancy
            else
                status = st!; // Inactive / Terminated

            inactive.Add(new D365InactiveWorkdayRowDto
            {
                UserName = u.UserName,
                EmployeeId = u.EmployeeId,
                WorkdayStatus = status,
                D365RoleCount = u.Roles.Count,
                Roles = string.Join("; ", u.Roles),
            });
        }
        inactive = inactive.OrderBy(r => r.WorkdayStatus).ThenBy(r => r.UserName).ToList();

        return Ok(new DiscrepanciesDto
        {
            Summary = new DiscrepancySummaryDto
            {
                GeneratedUtc = DateTime.UtcNow,
                DynawayLicensesTotal = dyn.Count,
                TremblantDynawayCount = tremblantDynaway.Count,
                NoActiveAdCount = noAd.Count,
                DynawayNoD365RoleCount = dynawayNoRole.Count,
                D365InactiveWorkdayCount = inactive.Count,
            },
            TremblantDynaway = tremblantDynaway,
            NoActiveAd = noAd,
            DynawayNoD365Role = dynawayNoRole,
            D365InactiveWorkday = inactive,
        });
    }

    private sealed record D365User(string UserName, string? EmployeeId, List<string> Roles);

    /// <summary>Case/accent-insensitive name key so "Éric X (T)" (AD CN) and "Eric X (T)" (D365
    /// export) line up. Keeps the "(T)" suffix — it's part of both names.</summary>
    private static string Norm(string s)
    {
        var d = s.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return Regex.Replace(sb.ToString().Normalize(NormalizationForm.FormC), @"\s+", " ").Trim();
    }
}
