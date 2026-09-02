using Microsoft.EntityFrameworkCore;
using TremblantLifecycle.Api.Data;
using TremblantLifecycle.Api.Models.Entities;

namespace TremblantLifecycle.Api.Services;

/// <summary>A DIFFERENT authorization table from AppUsers — see D365Approver's doc comment for why.
/// Matching reuses AppUserService.Normalize's bare/lowercased/domain-stripped sAMAccountName rule so
/// a person's access survives the iDirectory -> ENTERPRISE migration the same way AppUsers does.</summary>
public interface ID365ApproverService
{
    /// <summary>True if this identity has ANY row at all (global or scoped) — gates seeing the "D365
    /// - Approbations" nav link and the tracking list; acting on a SPECIFIC request is a separate,
    /// narrower check (see CanActOnAsync).</summary>
    Task<bool> HasAnyAccessAsync(string? identityName, CancellationToken ct);

    /// <summary>True if this identity may complete an approval for an employee whose Workday
    /// Position_Title is <paramref name="positionTitle"/> — a global approver (no PositionTitle on
    /// their row) can act on anything; a scoped approver only on a matching title.</summary>
    Task<bool> CanActOnAsync(string? identityName, string? positionTitle, CancellationToken ct);

    Task<List<D365Approver>> ListAsync(CancellationToken ct);

    /// <summary>Every approver eligible to act on a request for this position title — global
    /// approvers plus those scoped to it — used to build the notification-email recipient list.</summary>
    Task<List<D365Approver>> MatchingAsync(string? positionTitle, CancellationToken ct);

    Task<D365Approver> AddAsync(string sam, string displayName, string? email, string? positionTitle, string? addedByDisplayName, CancellationToken ct);
    Task<bool> RemoveAsync(int d365ApproverId, CancellationToken ct);
}

public class D365ApproverService : ID365ApproverService
{
    private readonly AppDbContext _db;

    public D365ApproverService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> HasAnyAccessAsync(string? identityName, CancellationToken ct)
    {
        var sam = AppUserService.Normalize(identityName);
        if (sam.Length == 0) return false;

        try
        {
            return await _db.D365Approvers.AsNoTracking().AnyAsync(a => a.Sam == sam, ct);
        }
        catch
        {
            // Tolerate the table not existing yet, same reasoning as AppUserService.GetRoleAsync.
            return false;
        }
    }

    public async Task<bool> CanActOnAsync(string? identityName, string? positionTitle, CancellationToken ct)
    {
        var sam = AppUserService.Normalize(identityName);
        if (sam.Length == 0) return false;

        return await _db.D365Approvers.AsNoTracking()
            .AnyAsync(a => a.Sam == sam && (a.PositionTitle == null || a.PositionTitle == positionTitle), ct);
    }

    public Task<List<D365Approver>> ListAsync(CancellationToken ct) =>
        _db.D365Approvers.AsNoTracking()
            .OrderBy(a => a.PositionTitle == null ? 0 : 1) // global approvers first
            .ThenBy(a => a.PositionTitle)
            .ThenBy(a => a.DisplayName)
            .ToListAsync(ct);

    public Task<List<D365Approver>> MatchingAsync(string? positionTitle, CancellationToken ct) =>
        _db.D365Approvers.AsNoTracking()
            .Where(a => a.PositionTitle == null || a.PositionTitle == positionTitle)
            .ToListAsync(ct);

    public async Task<D365Approver> AddAsync(string sam, string displayName, string? email, string? positionTitle, string? addedByDisplayName, CancellationToken ct)
    {
        var normalized = AppUserService.Normalize(sam);
        var scope = string.IsNullOrWhiteSpace(positionTitle) ? null : positionTitle.Trim();

        var existing = await _db.D365Approvers.FirstOrDefaultAsync(a => a.Sam == normalized && a.PositionTitle == scope, ct);
        if (existing is not null)
        {
            existing.DisplayName = displayName;
            existing.Email = email;
            await _db.SaveChangesAsync(ct);
            return existing;
        }

        var approver = new D365Approver
        {
            Sam = normalized,
            DisplayName = displayName,
            Email = email,
            PositionTitle = scope,
            CreatedAt = DateTime.UtcNow,
            CreatedByDisplayName = addedByDisplayName
        };
        _db.D365Approvers.Add(approver);
        await _db.SaveChangesAsync(ct);
        return approver;
    }

    public async Task<bool> RemoveAsync(int d365ApproverId, CancellationToken ct)
    {
        var approver = await _db.D365Approvers.FirstOrDefaultAsync(a => a.D365ApproverId == d365ApproverId, ct);
        if (approver is null) return true;

        _db.D365Approvers.Remove(approver);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
