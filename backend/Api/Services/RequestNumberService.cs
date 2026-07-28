using Microsoft.EntityFrameworkCore;
using TremblantLifecycle.Api.Data;
using TremblantLifecycle.Api.Models.Entities;

namespace TremblantLifecycle.Api.Services;

/// <summary>Generates request numbers like "INT-2026-00024" using the SQL SEQUENCE defined in
/// AppDbContext (RequestNumberSeq) — transactionally safe, avoids the race condition an app-side
/// MAX()+1 would have under concurrent submissions.</summary>
public class RequestNumberService
{
    private readonly AppDbContext _db;

    public RequestNumberService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<string> GenerateAsync(RequestType type, CancellationToken ct = default)
    {
        var prefix = type switch
        {
            RequestType.Onboarding => "INT",
            RequestType.Reactivation => "REA",
            RequestType.Offboarding => "TER",
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

        var next = await _db.Database
            .SqlQuery<int>($"SELECT NEXT VALUE FOR RequestNumberSeq AS Value")
            .SingleAsync(ct);

        return $"{prefix}-{DateTime.UtcNow.Year}-{next:D5}";
    }
}
