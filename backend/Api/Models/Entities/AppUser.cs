namespace TremblantLifecycle.Api.Models.Entities;

/// <summary>App-managed admin role — separate from AD groups, matching the pattern used by other
/// apps in this family (PO Tool, PassManagement). Existence of a row for a given email means that
/// person is a Ticket Template admin (can edit what information gets written into the tickets this
/// app creates, and manage who else has this role). Bootstrapped with a single seeded row; managed
/// afterward via the in-app "Administrateurs" screen.</summary>
public class AppUser
{
    public int AppUserId { get; set; }
    public string Email { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public string? CreatedByDisplayName { get; set; }
}
