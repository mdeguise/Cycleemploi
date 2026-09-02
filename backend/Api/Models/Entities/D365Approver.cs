namespace TremblantLifecycle.Api.Models.Entities;

/// <summary>An Enterprise.AD person allowed to fill out a D365 access-approval request — see
/// D365AccessApproval. Matched the same way as AppUser (bare, lowercased, domain-stripped
/// sAMAccountName — see AppUserService.Normalize), but this is a DIFFERENT authorization table:
/// an approver does not need any AppUsers row to use the approval screen, and an AppUsers admin
/// does not automatically become an approver.
///
/// PositionTitle scopes an approver to only the requests where the employee's Workday
/// Position_Title matches exactly (e.g. an AP-specific approver who should only see AP-flavoured
/// roles) — null means global, able to act on any pending approval.</summary>
public class D365Approver
{
    public int D365ApproverId { get; set; }

    /// <summary>Bare sAMAccountName — the authorization key, same normalization as AppUser.Sam.</summary>
    public string Sam { get; set; } = null!;

    public string DisplayName { get; set; } = null!;
    public string? Email { get; set; }

    /// <summary>Null = global approver (any request). Set = only requests where the employee's
    /// Workday Position_Title equals this value exactly.</summary>
    public string? PositionTitle { get; set; }

    public DateTime CreatedAt { get; set; }
    public string? CreatedByDisplayName { get; set; }
}
