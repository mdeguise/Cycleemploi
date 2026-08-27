namespace TremblantLifecycle.Api.Models.Entities;

/// <summary>What an <see cref="AppUser"/> row is allowed to do. Stored as a string in SQL (not an
/// int) so the table stays readable to a DBA and so inserting a new role later can't silently
/// renumber the existing ones.</summary>
public enum AppUserRole
{
    /// <summary>Read-only access to the Administration section: can view every request and the
    /// tickets each one produced, but cannot retry ticket creation, edit ticket templates, or
    /// manage this list.</summary>
    Lecteur = 0,

    /// <summary>Full access: everything Lecteur can do, plus retrying failed ticket creation,
    /// editing ticket templates, and adding/removing app users.</summary>
    Admin = 1
}

/// <summary>App-managed access control — deliberately NOT an AD group, matching the pattern used by
/// the other apps in this family (PO Tool, PassManagement, SEI KPI). One row per person who has any
/// access to the Administration section; <see cref="Role"/> says how much.
///
/// Matched on <see cref="Sam"/> — the BARE sAMAccountName, domain stripped and lowercased — rather
/// than on email or on a domain-qualified name. This is deliberate and was learned the hard way:
///   * Email breaks for accounts whose AD `mail` attribute is empty (every *_adm admin account at
///     Tremblant), which silently made those accounts un-adminable.
///   * A domain-qualified name (IDIRECTORY\x vs ENTERPRISE\x) breaks the moment a person is
///     migrated between domains — the iDirectory.itw -> ENTERPRISE.AD migration did exactly that.
/// A bare sam survives both. See also the Access:BootstrapAdmins setting, which is the recovery
/// path if this table is ever emptied or every row stops matching.</summary>
public class AppUser
{
    public int AppUserId { get; set; }

    /// <summary>Bare sAMAccountName, lowercase, no domain prefix (e.g. "mdeguise", "mdeguise_adm").
    /// Always write it through AppUserService.Normalize so the stored form matches what the lookup
    /// computes from the caller's Windows identity.</summary>
    public string Sam { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    /// <summary>Informational only — shown in the Administrateurs screen so a human can tell two
    /// similarly-named accounts apart. Never used for authorization (see the class doc comment) and
    /// nullable because admin accounts routinely have no `mail` attribute in AD.</summary>
    public string? Email { get; set; }

    public AppUserRole Role { get; set; } = AppUserRole.Admin;

    public DateTime CreatedAt { get; set; }
    public string? CreatedByDisplayName { get; set; }
}
