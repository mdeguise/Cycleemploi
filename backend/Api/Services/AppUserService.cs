using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TremblantLifecycle.Api.Data;
using TremblantLifecycle.Api.Models.Entities;

namespace TremblantLifecycle.Api.Services;

/// <summary>Bootstrap access, configured rather than stored, so there is always a way back in if the
/// AppUsers table is emptied or every row stops matching after a domain migration. Comma-separated
/// bare sAMAccountNames; each one is always treated as an Admin whether or not it has a row.</summary>
public class AccessOptions
{
    public string BootstrapAdmins { get; set; } = "";
}

public class AppUserService : IAppUserService
{
    private readonly AppDbContext _db;
    private readonly HashSet<string> _bootstrapAdmins;

    public AppUserService(AppDbContext db, IOptions<AccessOptions> access)
    {
        _db = db;
        _bootstrapAdmins = (access.Value.BootstrapAdmins ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Normalize)
            .Where(s => s.Length > 0)
            .ToHashSet();
    }

    /// <summary>"ENTERPRISE\mdeguise_adm" / "IDIRECTORY\mdeguise" / "mdeguise@tremblant.ca" -> "mdeguise_adm" / "mdeguise".
    /// Domain-agnostic on purpose — a person keeps their access across the iDirectory -> ENTERPRISE
    /// migration. Note "mdeguise" and "mdeguise_adm" are correctly treated as DIFFERENT people: an
    /// admin account is a separate identity and must be granted access explicitly.</summary>
    public static string Normalize(string? identityName)
    {
        if (string.IsNullOrWhiteSpace(identityName)) return "";
        var name = identityName.Trim();
        var slash = name.IndexOf('\\');
        if (slash >= 0) name = name[(slash + 1)..];
        var at = name.IndexOf('@');
        if (at >= 0) name = name[..at];
        return name.ToLowerInvariant();
    }

    public async Task<AppUserRole?> GetRoleAsync(string? identityName, CancellationToken ct)
    {
        var sam = Normalize(identityName);
        if (sam.Length == 0) return null;
        if (_bootstrapAdmins.Contains(sam)) return AppUserRole.Admin;

        // Tolerate the table not existing yet (before the migration is applied) rather than throwing:
        // bootstrap admins must still be able to get in and fix things.
        try
        {
            var row = await _db.AppUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Sam == sam, ct);
            return row?.Role;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> IsAdminAsync(string? identityName, CancellationToken ct) =>
        await GetRoleAsync(identityName, ct) == AppUserRole.Admin;

    public async Task<bool> HasAnyAccessAsync(string? identityName, CancellationToken ct) =>
        await GetRoleAsync(identityName, ct) is not null;

    public async Task<List<AppUser>> ListAsync(CancellationToken ct) =>
        await _db.AppUsers.AsNoTracking()
            .OrderByDescending(u => u.Role)
            .ThenBy(u => u.DisplayName)
            .ToListAsync(ct);

    public async Task<AppUser> AddAsync(string sam, string displayName, string? email, AppUserRole role, string? addedByDisplayName, CancellationToken ct)
    {
        var normalized = Normalize(sam);

        var existing = await _db.AppUsers.FirstOrDefaultAsync(u => u.Sam == normalized, ct);
        if (existing is not null)
        {
            // Re-adding someone who is already listed promotes/demotes them instead of failing or
            // creating a duplicate row that the unique index would reject anyway.
            existing.Role = role;
            existing.DisplayName = displayName;
            existing.Email = email;
            await _db.SaveChangesAsync(ct);
            return existing;
        }

        var user = new AppUser
        {
            Sam = normalized,
            DisplayName = displayName,
            Email = email,
            Role = role,
            CreatedAt = DateTime.UtcNow,
            CreatedByDisplayName = addedByDisplayName
        };
        _db.AppUsers.Add(user);
        await _db.SaveChangesAsync(ct);
        return user;
    }

    public async Task<AppUser?> UpdateRoleAsync(int appUserId, AppUserRole role, CancellationToken ct)
    {
        var user = await _db.AppUsers.FirstOrDefaultAsync(u => u.AppUserId == appUserId, ct);
        if (user is null) return null;

        // Demoting the last Admin would leave nobody able to manage the list (bootstrap admins
        // aside, and those are only set on the server) — refuse, same rule as RemoveAsync.
        if (user.Role == AppUserRole.Admin && role != AppUserRole.Admin && await IsLastAdminAsync(appUserId, ct))
        {
            return null;
        }

        user.Role = role;
        await _db.SaveChangesAsync(ct);
        return user;
    }

    public async Task<bool> RemoveAsync(int appUserId, CancellationToken ct)
    {
        var user = await _db.AppUsers.FirstOrDefaultAsync(u => u.AppUserId == appUserId, ct);
        if (user is null) return true;

        if (user.Role == AppUserRole.Admin && await IsLastAdminAsync(appUserId, ct)) return false;

        _db.AppUsers.Remove(user);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<bool> IsLastAdminAsync(int appUserId, CancellationToken ct) =>
        !await _db.AppUsers.AnyAsync(u => u.Role == AppUserRole.Admin && u.AppUserId != appUserId, ct);
}
