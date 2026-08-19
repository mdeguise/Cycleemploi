using Microsoft.EntityFrameworkCore;
using TremblantLifecycle.Api.Data;
using TremblantLifecycle.Api.Models.Entities;

namespace TremblantLifecycle.Api.Services;

public class AppUserService : IAppUserService
{
    private readonly AppDbContext _db;

    public AppUserService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> IsAdminAsync(string? email, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        return await _db.AppUsers.AnyAsync(u => u.Email.ToLower() == email.ToLower(), ct);
    }

    public async Task<List<AppUser>> ListAdminsAsync(CancellationToken ct) =>
        await _db.AppUsers.OrderBy(u => u.DisplayName).ToListAsync(ct);

    public async Task<AppUser> AddAdminAsync(string email, string displayName, string? addedByDisplayName, CancellationToken ct)
    {
        var existing = await _db.AppUsers.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower(), ct);
        if (existing is not null) return existing;

        var user = new AppUser
        {
            Email = email,
            DisplayName = displayName,
            CreatedAt = DateTime.UtcNow,
            CreatedByDisplayName = addedByDisplayName
        };
        _db.AppUsers.Add(user);
        await _db.SaveChangesAsync(ct);
        return user;
    }

    public async Task RemoveAdminAsync(int appUserId, CancellationToken ct)
    {
        var user = await _db.AppUsers.FindAsync([appUserId], ct);
        if (user is null) return;

        _db.AppUsers.Remove(user);
        await _db.SaveChangesAsync(ct);
    }
}
