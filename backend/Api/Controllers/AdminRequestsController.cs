using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TremblantLifecycle.Api.Data;
using TremblantLifecycle.Api.Models.Dtos;
using TremblantLifecycle.Api.Models.Entities;
using TremblantLifecycle.Api.Services;

namespace TremblantLifecycle.Api.Controllers;

/// <summary>The Administration section's view over every request and the tickets it produced.
///
/// Separate from RequestsController on purpose: that controller is the requester-facing wizard, and
/// its authorization model is "the person who created this draft". This one is
/// administrator-facing, sees ALL requests regardless of who submitted them, and is gated on the
/// AppUsers table instead. Keeping them apart means a change to one cannot accidentally widen the
/// other.</summary>
[ApiController]
[Route("api/admin/requests")]
[Authorize]
public class AdminRequestsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAppUserService _appUsers;
    private readonly IRequestTicketService _tickets;
    private readonly ITicketOrchestrationService _orchestration;
    private readonly ILogger<AdminRequestsController> _logger;

    public AdminRequestsController(
        AppDbContext db,
        IAppUserService appUsers,
        IRequestTicketService tickets,
        ITicketOrchestrationService orchestration,
        ILogger<AdminRequestsController> logger)
    {
        _db = db;
        _appUsers = appUsers;
        _tickets = tickets;
        _orchestration = orchestration;
        _logger = logger;
    }

    /// <summary>Lecteur or Admin — enough to look, not to act.</summary>
    private Task<bool> CanViewAsync(CancellationToken ct) => _appUsers.HasAnyAccessAsync(User.GetObjectId(), ct);

    /// <summary>Admin only. Retrying creates something in a real external system, which is not a
    /// read-only action and therefore not something a Lecteur may do.</summary>
    private Task<bool> CanRetryAsync(CancellationToken ct) => _appUsers.IsAdminAsync(User.GetObjectId(), ct);

    private static RequestTicketDto ToDto(RequestTicket t, IReadOnlyDictionary<int, string> employeeNames) => new()
    {
        RequestTicketId = t.RequestTicketId,
        Kind = t.Kind.ToString(),
        KindLabel = TicketKindLabels.For(t.Kind),
        Outcome = t.Outcome.ToString(),
        RequestEmployeeId = t.RequestEmployeeId,
        EmployeeName = t.RequestEmployeeId is { } id && employeeNames.TryGetValue(id, out var name) ? name : null,
        TicketNumber = t.TicketNumber,
        ErrorType = t.ErrorType,
        ErrorMessage = t.ErrorMessage,
        AttemptCount = t.AttemptCount,
        FirstAttemptAt = t.FirstAttemptAt,
        LastAttemptAt = t.LastAttemptAt
    };

    [HttpGet("{id:int}/tickets")]
    public async Task<ActionResult<List<RequestTicketDto>>> Tickets(int id, CancellationToken ct)
    {
        if (!await CanViewAsync(ct)) return Forbid();

        var employeeNames = await _db.RequestEmployees.AsNoTracking()
            .Where(e => e.RequestId == id)
            .ToDictionaryAsync(e => e.RequestEmployeeId, e => e.NameSnapshot, ct);

        var rows = await _tickets.ListForRequestAsync(id, ct);
        return Ok(rows.Select(t => ToDto(t, employeeNames)).ToList());
    }

    /// <summary>Re-runs the ONE integration this ticket row represents.
    ///
    /// Idempotent by construction: it acts on a single (request, kind, employee) row and the unique
    /// index behind that row means a retry updates it rather than creating a second ticket. This is
    /// deliberately NOT "submit the request again" — submitting fires all four integrations
    /// unconditionally, which on #INT-2026-00053 would have duplicated the two D365 records that
    /// had actually succeeded while fixing the two that failed.</summary>
    [HttpPost("{id:int}/tickets/{ticketId:int}/retry")]
    public async Task<ActionResult<RetryTicketResultDto>> Retry(int id, int ticketId, CancellationToken ct)
    {
        if (!await CanRetryAsync(ct)) return Forbid();

        var ticket = await _db.RequestTickets.AsNoTracking()
            .FirstOrDefaultAsync(t => t.RequestTicketId == ticketId && t.RequestId == id, ct);
        if (ticket is null) return NotFound();

        if (ticket.Outcome == TicketOutcome.Created)
        {
            // Refused rather than silently ignored: an administrator clicking Réessayer on a ticket
            // that already exists is a misunderstanding worth surfacing, not a no-op to hide.
            return Conflict(new RetryTicketResultDto
            {
                Succeeded = false,
                Error = $"Ce billet a déjà été créé ({ticket.TicketNumber}). Relancer le créerait en double."
            });
        }

        var request = await _db.Requests
            .Include(r => r.Employees)
            .Include(r => r.OnboardingDetail)
            .Include(r => r.OffboardingDetail)
            .Include(r => r.AccessDetail).ThenInclude(a => a!.Systemes)
            .Include(r => r.AccessDetail).ThenInclude(a => a!.PosHebergement)
            .Include(r => r.EquipmentDetail).ThenInclude(e => e!.Equipements)
            .Include(r => r.ApplicationsDetail).ThenInclude(a => a!.Applications)
            .Include(r => r.ConfidentialComment)
            .FirstOrDefaultAsync(r => r.RequestId == id, ct);
        if (request is null) return NotFound();

        _logger.LogInformation("Admin {Admin} is retrying {Kind} for request {RequestNumber}", User.GetObjectId(), ticket.Kind, request.RequestNumber);

        var result = await _orchestration.RetryAsync(request, ticket, ct);

        return Ok(new RetryTicketResultDto
        {
            Succeeded = result.Succeeded,
            TicketNumber = result.TicketNumber,
            Error = result.Error
        });
    }
}
