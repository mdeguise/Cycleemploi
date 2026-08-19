using TremblantLifecycle.Api.Models.Entities;

namespace TremblantLifecycle.Api.Services;

/// <summary>Manages the app-managed "Ticket Template admin" role — see AppUser's doc comment.</summary>
public interface IAppUserService
{
    Task<bool> IsAdminAsync(string? email, CancellationToken ct);
    Task<List<AppUser>> ListAdminsAsync(CancellationToken ct);
    Task<AppUser> AddAdminAsync(string email, string displayName, string? addedByDisplayName, CancellationToken ct);
    Task RemoveAdminAsync(int appUserId, CancellationToken ct);
}
