namespace TremblantLifecycle.Api.Models.Dtos;

/// <summary>Everything the reconciliation ("Écarts") page needs, in one payload.</summary>
public class DiscrepanciesDto
{
    public DiscrepancySummaryDto Summary { get; set; } = new();
    public List<TremblantDynawayRowDto> TremblantDynaway { get; set; } = new();
    public List<NoActiveAdRowDto> NoActiveAd { get; set; } = new();
    public List<DynawayNoRoleRowDto> DynawayNoD365Role { get; set; } = new();
    public List<D365InactiveWorkdayRowDto> D365InactiveWorkday { get; set; } = new();
}

public class DiscrepancySummaryDto
{
    public DateTime GeneratedUtc { get; set; }
    public int DynawayLicensesTotal { get; set; }
    public int TremblantDynawayCount { get; set; }
    public int NoActiveAdCount { get; set; }
    public int DynawayNoD365RoleCount { get; set; }
    public int D365InactiveWorkdayCount { get; set; }
}

/// <summary>#1 — Dynaway licenses confirmed Tremblant via AD (extensionAttribute2 = "T").</summary>
public class TremblantDynawayRowDto
{
    public string? Name { get; set; }
    public string? Login { get; set; }
    public bool AdEnabled { get; set; }
    public bool HasD365Role { get; set; }
    public int D365RoleCount { get; set; }
}

/// <summary>#2a — accounts referenced by Dynaway/D365 that have no active AD account.</summary>
public class NoActiveAdRowDto
{
    public string Source { get; set; } = "";   // "Dynaway (T)" or "D365"
    public string Name { get; set; } = "";
    public string? Login { get; set; }
    public string Status { get; set; } = "";    // "Disabled" or "No AD account"
}

/// <summary>#2b — Tremblant Dynaway license holders with no D365 security role.</summary>
public class DynawayNoRoleRowDto
{
    public string? Name { get; set; }
    public string? Login { get; set; }
    public bool AdEnabled { get; set; }
}

/// <summary>#2c — D365 users whose Workday demographic is not Active (Inactive/Terminated), or has
/// no resolvable Workday record.</summary>
public class D365InactiveWorkdayRowDto
{
    public string UserName { get; set; } = "";
    public string? EmployeeId { get; set; }
    public string WorkdayStatus { get; set; } = ""; // "Inactive", "Terminated", "No Workday record", "Not linked"
    public int D365RoleCount { get; set; }
    public string Roles { get; set; } = "";
}
