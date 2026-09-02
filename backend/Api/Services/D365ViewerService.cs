using Microsoft.EntityFrameworkCore;
using TremblantLifecycle.Api.Data;
using TremblantLifecycle.Api.Models.Entities;

namespace TremblantLifecycle.Api.Services;

/// <summary>A DIFFERENT authorization table from both AppUsers and D365Approver — see D365Viewer's
/// doc comment. Matching reuses AppUserService.Normalize, same as D365ApproverService.</summary>
public interface ID365ViewerService
{
    Task<bool> HasAnyAccessAsync(string? identityName, CancellationToken ct);
    Task<List<D365Viewer>> ListAsync(CancellationToken ct);
    Task<D365Viewer> AddAsync(string sam, string displayName, string? email, string? addedByDisplayName, CancellationToken ct);
    Task<bool> RemoveAsync(int d365ViewerId, CancellationToken ct);
}

public class D365ViewerService : ID365ViewerService
{
    private readonly AppDbContext _db;

    public D365ViewerService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> HasAnyAccessAsync(string? identityName, CancellationToken ct)
    {
        var sam = AppUserService.Normalize(identityName);
        if (sam.Length == 0) return false;

        try
        {
            return await _db.D365Viewers.AsNoTracking().AnyAsync(v => v.Sam == sam, ct);
        }
        catch
        {
            // Tolerate the table not existing yet, same reasoning as AppUserService.GetRoleAsync.
            return false;
        }
    }

    public Task<List<D365Viewer>> ListAsync(CancellationToken ct) =>
        _db.D365Viewers.AsNoTracking().OrderBy(v => v.DisplayName).ToListAsync(ct);

    public async Task<D365Viewer> AddAsync(string sam, string displayName, string? email, string? addedByDisplayName, CancellationToken ct)
    {
        var normalized = AppUserService.Normalize(sam);

        var existing = await _db.D365Viewers.FirstOrDefaultAsync(v => v.Sam == normalized, ct);
        if (existing is not null)
        {
            existing.DisplayName = displayName;
            existing.Email = email;
            await _db.SaveChangesAsync(ct);
            return existing;
        }

        var viewer = new D365Viewer
        {
            Sam = normalized,
            DisplayName = displayName,
            Email = email,
            CreatedAt = DateTime.UtcNow,
            CreatedByDisplayName = addedByDisplayName
        };
        _db.D365Viewers.Add(viewer);
        await _db.SaveChangesAsync(ct);
        return viewer;
    }

    public async Task<bool> RemoveAsync(int d365ViewerId, CancellationToken ct)
    {
        var viewer = await _db.D365Viewers.FirstOrDefaultAsync(v => v.D365ViewerId == d365ViewerId, ct);
        if (viewer is null) return true;

        _db.D365Viewers.Remove(viewer);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
