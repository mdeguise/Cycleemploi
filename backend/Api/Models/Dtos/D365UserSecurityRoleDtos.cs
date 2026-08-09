namespace TremblantLifecycle.Api.Models.Dtos;

public class D365UserSecurityRoleDto
{
    public int Id { get; set; }
    public string UserName { get; set; } = null!;
    public string SecurityRole { get; set; } = null!;
    public string? EmployeeId { get; set; }
    public string? JobCode { get; set; }
    public string? PositionTitle { get; set; }
}

/// <summary>Linking a row to the correct employee only requires the EmployeeId — JobCode and
/// PositionTitle are re-resolved from WorkdayDemographic server-side (PrimaryJob == 1) rather than
/// accepted from the client, so they always reflect Workday's actual current data.</summary>
public class LinkD365UserSecurityRoleDto
{
    public string EmployeeId { get; set; } = null!;
}
