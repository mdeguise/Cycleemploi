using TremblantLifecycle.Api.Models.Entities;

namespace TremblantLifecycle.Api.Services;

/// <summary>Manages app-managed access to the Administration section — see <see cref="AppUser"/>.
/// Every method that takes an identity accepts the RAW Windows identity name (e.g.
/// "ENTERPRISE\mdeguise_adm") and normalizes it internally, so callers never have to remember to
/// strip the domain or to look the person's email up in AD first.</summary>
public interface IAppUserService
{
    /// <summary>The caller's role, or null if they have no access at all. Bootstrap admins always
    /// resolve to <see cref="AppUserRole.Admin"/> even with no row in the table.</summary>
    Task<AppUserRole?> GetRoleAsync(string? identityName, CancellationToken ct);

    /// <summary>True when the caller may retry tickets, edit templates and manage app users.</summary>
    Task<bool> IsAdminAsync(string? identityName, CancellationToken ct);

    /// <summary>True when the caller may at least view the Administration section.</summary>
    Task<bool> HasAnyAccessAsync(string? identityName, CancellationToken ct);

    Task<List<AppUser>> ListAsync(CancellationToken ct);

    Task<AppUser> AddAsync(string sam, string displayName, string? email, AppUserRole role, string? addedByDisplayName, CancellationToken ct);

    Task<AppUser?> UpdateRoleAsync(int appUserId, AppUserRole role, CancellationToken ct);

    /// <summary>Removes a row. Returns false when the caller tried to remove the last remaining
    /// Admin — refused, because it would leave nobody able to manage the list.</summary>
    Task<bool> RemoveAsync(int appUserId, CancellationToken ct);
}
