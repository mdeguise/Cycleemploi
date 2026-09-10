namespace TremblantLifecycle.Api.Models.Entities;

/// <summary>An Enterprise.AD person allowed to act on a D365 access-approval request at one of the
/// three fixed stages — see D365AccessApproval. Matched the same way as AppUser (bare, lowercased,
/// domain-stripped sAMAccountName — see AppUserService.Normalize), but this is a DIFFERENT
/// authorization table: an approver does not need any AppUsers row to use the approval screen, and
/// an AppUsers admin does not automatically become an approver.
///
/// Replaces the earlier Workday-Position_Title-based scoping (an approver used to be either global
/// or scoped to one exact Position_Title) with a fixed routing role instead — see
/// D365ApprovalRoles. Several people can hold the same role (e.g. Stage1 currently has two), and
/// any one of them acting is enough to advance that stage.</summary>
public class D365Approver
{
    public int D365ApproverId { get; set; }

    /// <summary>Bare sAMAccountName — the authorization key, same normalization as AppUser.Sam.</summary>
    public string Sam { get; set; } = null!;

    public string DisplayName { get; set; } = null!;
    public string? Email { get; set; }

    /// <summary>One of D365ApprovalRoles.All — which stage this person may act on.</summary>
    public string ApprovalRole { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
    public string? CreatedByDisplayName { get; set; }
}
