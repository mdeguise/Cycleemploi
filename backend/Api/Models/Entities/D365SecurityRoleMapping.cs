namespace TremblantLifecycle.Api.Models.Entities;

/// <summary>Maps a Workday job code to one or more D365 security roles — a job code can need
/// several roles (e.g. an accountant needing both AP and GL access). Used when a request selects
/// "Accès D365" to resolve which roles to request on the TDX "D365 - Access" ticket (FormID 10799),
/// by looking up the employee's job code(s). Role must be the exact real D365 role name (e.g.
/// "AMC_GL_Preparer"), not an abstracted label — see D365SecurityRolesController's Catalog endpoint,
/// which sources valid values from D365UserSecurityRoles (a real export of current assignments).
/// Managed via a small admin page in the app; started from a derived base (roles held by >= 30% of
/// employees currently in each job code) and refined by hand from there.</summary>
public class D365SecurityRoleMapping
{
    public int Id { get; set; }
    public string JobCode { get; set; } = null!;
    public string Role { get; set; } = null!;
}
