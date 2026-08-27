using Microsoft.EntityFrameworkCore;
using TremblantLifecycle.Api.Data;
using TremblantLifecycle.Api.Models.Entities;

namespace TremblantLifecycle.Api.Services;

/// <summary>Records what each downstream integration produced for a request — see
/// <see cref="RequestTicket"/> for why this exists at all.</summary>
public interface IRequestTicketService
{
    Task RecordSuccessAsync(int requestId, TicketKind kind, int? requestEmployeeId, string? ticketNumber, CancellationToken ct);
    Task RecordFailureAsync(int requestId, TicketKind kind, int? requestEmployeeId, Exception ex, CancellationToken ct);
    Task<List<RequestTicket>> ListForRequestAsync(int requestId, CancellationToken ct);
}

public class RequestTicketService : IRequestTicketService
{
    private readonly AppDbContext _db;
    private readonly ILogger<RequestTicketService> _logger;

    public RequestTicketService(AppDbContext db, ILogger<RequestTicketService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public Task RecordSuccessAsync(int requestId, TicketKind kind, int? requestEmployeeId, string? ticketNumber, CancellationToken ct) =>
        UpsertAsync(requestId, kind, requestEmployeeId, row =>
        {
            row.Outcome = TicketOutcome.Created;
            row.TicketNumber = ticketNumber;
            row.ErrorType = null;
            row.ErrorMessage = null;
        }, ct);

    public Task RecordFailureAsync(int requestId, TicketKind kind, int? requestEmployeeId, Exception ex, CancellationToken ct) =>
        UpsertAsync(requestId, kind, requestEmployeeId, row =>
        {
            row.Outcome = TicketOutcome.Failed;
            row.ErrorType = ex.GetType().Name;
            // Include the inner exception: the outer message is often a generic wrapper, and the
            // useful detail (an HTTP status, a SQL cast failure) lives underneath.
            row.ErrorMessage = Truncate(ex.InnerException is not null ? $"{ex.Message} — {ex.InnerException.Message}" : ex.Message, 2000);
        }, ct);

    public Task<List<RequestTicket>> ListForRequestAsync(int requestId, CancellationToken ct) =>
        _db.RequestTickets.AsNoTracking()
            .Where(t => t.RequestId == requestId)
            .OrderBy(t => t.Kind)
            .ThenBy(t => t.RequestEmployeeId)
            .ToListAsync(ct);

    /// <summary>One row per (request, kind, employee) — a retry updates in place rather than adding
    /// a second row, which is what keeps the ticket history readable and the retry idempotent.
    ///
    /// Recording NEVER throws. This is called from the best-effort integration paths that run after
    /// a submission is already committed; letting a bookkeeping failure escape would turn "the
    /// ticket was created" into "the submission looks broken to the requester".</summary>
    private async Task UpsertAsync(int requestId, TicketKind kind, int? requestEmployeeId, Action<RequestTicket> apply, CancellationToken ct)
    {
        try
        {
            var now = DateTime.UtcNow;
            var row = await _db.RequestTickets
                .FirstOrDefaultAsync(t => t.RequestId == requestId && t.Kind == kind && t.RequestEmployeeId == requestEmployeeId, ct);

            if (row is null)
            {
                row = new RequestTicket
                {
                    RequestId = requestId,
                    Kind = kind,
                    RequestEmployeeId = requestEmployeeId,
                    FirstAttemptAt = now,
                    AttemptCount = 0
                };
                _db.RequestTickets.Add(row);
            }

            row.AttemptCount++;
            row.LastAttemptAt = now;
            apply(row);

            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record ticket outcome ({Kind}, employee {RequestEmployeeId}) for request {RequestId}", kind, requestEmployeeId, requestId);
        }
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
