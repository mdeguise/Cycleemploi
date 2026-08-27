using Microsoft.Extensions.Caching.Memory;
using TremblantLifecycle.Api.Models.Entities;

namespace TremblantLifecycle.Api.Services;

/// <summary>Resolves the CURRENT state of tickets in Freshdesk and TDX — information this app does
/// not own. RequestTicket records whether creation succeeded; whether the ticket was since worked
/// and closed only exists in the source system.</summary>
public interface ITicketStatusService
{
    /// <summary>Live status per RequestTicketId. Only queries tickets that were actually created and
    /// carry a usable number; everything else comes back Unknown.</summary>
    Task<IReadOnlyDictionary<int, LiveTicketStatus>> GetStatusesAsync(IReadOnlyCollection<RequestTicket> tickets, CancellationToken ct);
}

public class TicketStatusService : ITicketStatusService
{
    private readonly IFreshdeskService _freshdesk;
    private readonly ITdxService _tdx;
    private readonly IMemoryCache _cache;
    private readonly ILogger<TicketStatusService> _logger;

    /// <summary>Short enough that someone closing a ticket sees it reflected within a couple of
    /// minutes, long enough that paging back and forth does not re-query every row.</summary>
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(3);

    /// <summary>Caps concurrent outbound calls. A page of 25 requests can reference well over a
    /// hundred tickets; firing those at Freshdesk and TDX simultaneously is how an integration user
    /// gets rate-limited or blocked.</summary>
    private const int MaxConcurrency = 6;

    public TicketStatusService(IFreshdeskService freshdesk, ITdxService tdx, IMemoryCache cache, ILogger<TicketStatusService> logger)
    {
        _freshdesk = freshdesk;
        _tdx = tdx;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>D365Badge is deliberately absent: it records a D365 job code, not a ticket in a
    /// ticketing system, so there is nothing to look up.</summary>
    private static bool IsFreshdesk(TicketKind k) =>
        k is TicketKind.Freshdesk or TicketKind.FreshdeskChildWithJobCodes or TicketKind.FreshdeskChildWithoutJobCodes;

    private static bool IsTdx(TicketKind k) => k is TicketKind.Tdx or TicketKind.D365Access;

    public async Task<IReadOnlyDictionary<int, LiveTicketStatus>> GetStatusesAsync(IReadOnlyCollection<RequestTicket> tickets, CancellationToken ct)
    {
        var result = new Dictionary<int, LiveTicketStatus>();
        var toFetch = new List<RequestTicket>();

        foreach (var t in tickets)
        {
            if (t.Outcome != TicketOutcome.Created || string.IsNullOrWhiteSpace(t.TicketNumber) || (!IsFreshdesk(t.Kind) && !IsTdx(t.Kind)))
            {
                result[t.RequestTicketId] = LiveTicketStatus.Unknown;
                continue;
            }

            if (_cache.TryGetValue<LiveTicketStatus>(CacheKey(t), out var cached) && cached is not null)
            {
                result[t.RequestTicketId] = cached;
                continue;
            }

            toFetch.Add(t);
        }

        if (toFetch.Count == 0) return result;

        using var gate = new SemaphoreSlim(MaxConcurrency);
        var fetched = await Task.WhenAll(toFetch.Select(async t =>
        {
            await gate.WaitAsync(ct);
            try
            {
                var status = await FetchAsync(t, ct);
                // Only cache a real answer. Caching Unknown would pin a transient outage in place
                // for the whole TTL, long after the system came back.
                if (status.State != LiveTicketState.Unknown)
                {
                    _cache.Set(CacheKey(t), status, CacheFor);
                }
                return (t.RequestTicketId, status);
            }
            finally
            {
                gate.Release();
            }
        }));

        foreach (var (id, status) in fetched) result[id] = status;
        return result;
    }

    private async Task<LiveTicketStatus> FetchAsync(RequestTicket t, CancellationToken ct)
    {
        try
        {
            if (IsFreshdesk(t.Kind) && long.TryParse(t.TicketNumber, out var fdId))
            {
                return await _freshdesk.GetTicketStatusAsync(fdId, ct);
            }
            if (IsTdx(t.Kind) && int.TryParse(t.TicketNumber, out var tdxId))
            {
                return await _tdx.GetTicketStatusAsync(tdxId, ct);
            }
        }
        catch (Exception ex)
        {
            // Both services promise not to throw, so reaching here means something unexpected —
            // still must not fail the page.
            _logger.LogWarning(ex, "Live status lookup failed for ticket {TicketNumber} ({Kind})", t.TicketNumber, t.Kind);
        }
        return LiveTicketStatus.Unknown;
    }

    private static string CacheKey(RequestTicket t) =>
        IsFreshdesk(t.Kind) ? $"ticketstatus:fd:{t.TicketNumber}" : $"ticketstatus:tdx:{t.TicketNumber}";
}
