using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TremblantLifecycle.Api.Data;
using TremblantLifecycle.Api.Models.Dtos;
using TremblantLifecycle.Api.Services;

namespace TremblantLifecycle.Api.Controllers;

/// <summary>Read-only reconciliation ("Écarts") view: cross-references the imported D365
/// security-role users, the imported Dynaway licenses, live Active Directory (Tremblant accounts),
/// and the read-only Workday demographic table to surface four kinds of discrepancy. Restricted to
/// the AD group TRM-CYCLEEMPLOI-D365-ADMIN — it exposes account-status data across the whole
/// Tremblant population.
///
/// People are matched across systems by <b>employeeID</b> (Workday EmployeeId == AD employeeID), not
/// by display name: D365/AD/Workday names disagree constantly (typos like "Alain Picard (T)s",
/// preferred names "Lyne"→"Lynn"/"Peter"→"Pete", maiden/married and hyphenated last names, and
/// cross-resort tags like (MTL)/(Alterra)/ext2=DEN on someone who nonetheless holds Tremblant D365
/// roles). Dynaway rows have no employeeID, so they still join via their AD login (User_ ==
/// sAMAccountName) → the AD account's employeeID.</summary>
[ApiController]
[Route("api/discrepancies")]
[Authorize]
public class DiscrepanciesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly WorkdayContext _workday;
    private readonly IAdDirectoryService _ad;
    private readonly string _adminGroup;

    public DiscrepanciesController(AppDbContext db, WorkdayContext workday, IAdDirectoryService ad,
        IOptions<HrGroupOptions> hr)
    {
        _db = db;
        _workday = workday;
        _ad = ad;
        _adminGroup = hr.Value.CycleEmploiD365AdminGroupName;
    }

    [HttpGet]
    public async Task<ActionResult<DiscrepanciesDto>> Get(CancellationToken ct)
    {
        var caller = User.Identity?.Name ?? "";
        if (!_ad.IsUserInGroup(caller, _adminGroup)) return Forbid();

        var d365 = await _db.D365UserSecurityRoles.AsNoTracking().ToListAsync(ct);
        var dyn = await _db.DynawayUsers.AsNoTracking().ToListAsync(ct);

        // Distinct D365 users (the source is one row per user+role), with their linked Workday
        // EmployeeId (null where the import couldn't confidently match the name — those live in the
        // separate "Corrections non liées" tab and are intentionally NOT judged here).
        var d365Users = d365
            .GroupBy(r => r.UserName)
            .Select(g => new D365User(
                g.Key,
                g.Select(x => x.EmployeeId).FirstOrDefault(x => !string.IsNullOrEmpty(x)),
                g.Select(x => x.SecurityRole).Distinct().OrderBy(x => x).ToList()))
            .ToList();
        var d365EmpIds = d365Users.Where(u => !string.IsNullOrEmpty(u.EmployeeId))
            .Select(u => u.EmployeeId!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var d365ByEmp = d365Users.Where(u => !string.IsNullOrEmpty(u.EmployeeId))
            .GroupBy(u => u.EmployeeId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        // Name index too, so a Dynaway holder still counts as "has a D365 role" when the matching
        // D365 row is unlinked (null EmployeeId) and can only be reached by name.
        var d365ByName = d365Users
            .GroupBy(u => Norm(u.UserName))
            .ToDictionary(g => g.Key, g => g.First());

        // Tremblant AD roster (ext2=T) — by login (Dynaway side) and by normalized CN (to reach an
        // unlinked D365 user's AD account, and through it their email → Workday).
        var adTremblant = _ad.GetTremblantAccounts();
        var adBySam = adTremblant
            .Where(a => !string.IsNullOrEmpty(a.Sam))
            .GroupBy(a => a.Sam.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First());
        var adByCn = adTremblant
            .Where(a => !string.IsNullOrEmpty(a.Cn))
            .GroupBy(a => Norm(a.Cn!))
            .ToDictionary(g => g.Key, g => g.First());

        // AD accounts for the D365 users, resolved by employeeID across the whole directory
        // (a Tremblant D365 user may have an AD account tagged another resort).
        var adByEmp = _ad.GetAccountsByEmployeeId(d365EmpIds)
            .Where(a => !string.IsNullOrEmpty(a.EmployeeId))
            .GroupBy(a => a.EmployeeId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // Workday employment status for the linked D365 users (by EmployeeId).
        var wdByEmp = (await _workday.WorkdayDemographics
                .Where(w => w.PrimaryJob == 1 && d365EmpIds.Contains(w.EmployeeId))
                .Select(w => new { w.EmployeeId, w.EmploymentStatus })
                .ToListAsync(ct))
            .GroupBy(w => w.EmployeeId)
            .ToDictionary(g => g.Key, g => g.First().EmploymentStatus);

        // Fallback path for #2c: unlinked D365 users (no EmployeeId) reached via their AD account's
        // email → Workday Work_Email. Resolves people whose D365↔Workday name match failed at import
        // but whose AD account we can find by name.
        var unlinkedEmails = d365Users
            .Where(u => string.IsNullOrEmpty(u.EmployeeId))
            .Select(u => adByCn.TryGetValue(Norm(u.UserName), out var a) ? a.Email : null)
            .Where(e => !string.IsNullOrEmpty(e))
            .Select(e => e!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var wdByEmail = unlinkedEmails.Count == 0
            ? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            : (await _workday.WorkdayDemographics
                    .Where(w => w.PrimaryJob == 1 && w.WorkEmail != null && unlinkedEmails.Contains(w.WorkEmail))
                    .Select(w => new { w.WorkEmail, w.EmploymentStatus })
                    .ToListAsync(ct))
                .GroupBy(w => w.WorkEmail!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().EmploymentStatus, StringComparer.OrdinalIgnoreCase);

        // ---- #1 Tremblant Dynaway (login resolves to a Tremblant AD account, ext2=T) ----
        var tremDyn = dyn
            .Where(d => !string.IsNullOrEmpty(d.Login) && adBySam.ContainsKey(d.Login!.ToLowerInvariant()))
            .Select(d => (Dyn: d, Ad: adBySam[d.Login!.ToLowerInvariant()]))
            .ToList();

        var tremblantDynaway = tremDyn.Select(t =>
        {
            var emp = t.Ad.EmployeeId;
            D365User? matched = null;
            if (!string.IsNullOrEmpty(emp)) d365ByEmp.TryGetValue(emp!, out matched);   // linked D365 rows: by ID
            if (matched is null && t.Ad.Cn is not null) d365ByName.TryGetValue(Norm(t.Ad.Cn), out matched); // unlinked: by name
            return new TremblantDynawayRowDto
            {
                Name = t.Ad.Cn ?? t.Dyn.Name,
                Login = t.Dyn.Login,
                AdEnabled = t.Ad.Enabled,
                HasD365Role = matched is not null,
                D365RoleCount = matched?.Roles.Count ?? 0,
            };
        }).OrderBy(r => r.Name).ToList();

        // ---- #2a No active AD account ----
        var noAd = new List<NoActiveAdRowDto>();
        foreach (var t in tremDyn)
            if (!t.Ad.Enabled)
                noAd.Add(new NoActiveAdRowDto { Source = "Dynaway (T)", Name = t.Ad.Cn ?? t.Dyn.Name ?? "", Login = t.Dyn.Login, Status = "Disabled" });
        foreach (var u in d365Users)
        {
            if (string.IsNullOrEmpty(u.EmployeeId)) continue; // unlinked → belongs to Corrections tab
            if (adByEmp.TryGetValue(u.EmployeeId!, out var a))
            {
                if (!a.Enabled)
                    noAd.Add(new NoActiveAdRowDto { Source = "D365", Name = a.Cn ?? u.UserName, Login = a.Sam, Status = "Disabled" });
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
            // Resolve the Workday record: linked EmployeeId first, else AD-account email → Work_Email.
            string? wdStatus = null;
            var resolved = false;
            var refEmpId = u.EmployeeId;
            if (!string.IsNullOrEmpty(u.EmployeeId))
            {
                resolved = wdByEmp.TryGetValue(u.EmployeeId!, out wdStatus);
            }
            else if (adByCn.TryGetValue(Norm(u.UserName), out var a) && !string.IsNullOrEmpty(a.Email)
                     && wdByEmail.TryGetValue(a.Email!, out wdStatus))
            {
                resolved = true;
                refEmpId = a.EmployeeId; // AD-derived id, useful for linking on the Corrections tab
            }

            string status;
            if (!resolved) status = string.IsNullOrEmpty(u.EmployeeId) ? "Not linked" : "No Workday record";
            else if (string.IsNullOrEmpty(wdStatus)) status = "No Workday record";
            else if (string.Equals(wdStatus, "Active", StringComparison.OrdinalIgnoreCase)) continue; // Active -> not a discrepancy
            else status = wdStatus!; // Inactive / Terminated

            inactive.Add(new D365InactiveWorkdayRowDto
            {
                UserName = u.UserName,
                EmployeeId = refEmpId,
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

    /// <summary>Case/accent-insensitive name key (keeps the "(T)" suffix) — only a fallback for
    /// matching a Dynaway holder to an <i>unlinked</i> D365 row; linked rows match by employeeID.</summary>
    private static string Norm(string s)
    {
        var d = s.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return string.Join(' ', sb.ToString().Normalize(NormalizationForm.FormC)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
