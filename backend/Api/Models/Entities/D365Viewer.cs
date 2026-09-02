namespace TremblantLifecycle.Api.Models.Entities;

/// <summary>An Enterprise.AD person allowed to SEE the D365 access-approval tracking list and
/// status of every request — "IT Personnel" — but not to act on one. A DIFFERENT authorization
/// table from both AppUsers and D365Approver, same reasoning as D365Approver's own doc comment:
/// nobody in this table needs an AppUsers row or a D365Approver row, and having one of those
/// doesn't automatically grant this. Always global (no PositionTitle scoping) — view access has
/// no reason to be narrower than "all requests".</summary>
public class D365Viewer
{
    public int D365ViewerId { get; set; }

    /// <summary>Bare sAMAccountName — the authorization key, same normalization as AppUser.Sam.</summary>
    public string Sam { get; set; } = null!;

    public string DisplayName { get; set; } = null!;
    public string? Email { get; set; }

    public DateTime CreatedAt { get; set; }
    public string? CreatedByDisplayName { get; set; }
}
