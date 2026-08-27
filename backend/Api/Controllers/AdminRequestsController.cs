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
/// Separate from RequestsController on purpose: that controller is the requester-facing wizard and
/// its authorization model is "the person who created this draft". This one is administrator-facing,
/// sees ALL requests regardless of who submitted them, and is gated on the AppUsers table instead.
/// Keeping them apart means a change to one cannot accidentally widen the other.</summary>
[ApiController]
[Route("api/admin/requests")]
[Authorize]
public class AdminRequestsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAppUserService _appUsers;
    private readonly IRequestTicketService _tickets;
    private readonly ITicketOrchestrationService _orchestration;
    private readonly RequestAuthorizationService _authz;
    private readonly ILogger<AdminRequestsController> _logger;

    public AdminRequestsController(
        AppDbContext db,
        IAppUserService appUsers,
        IRequestTicketService tickets,
        ITicketOrchestrationService orchestration,
        RequestAuthorizationService authz,
        ILogger<AdminRequestsController> logger)
    {
        _db = db;
        _appUsers = appUsers;
        _tickets = tickets;
        _orchestration = orchestration;
        _authz = authz;
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

    /// <summary>Every request, newest first, with a rolled-up ticket state so an administrator can
    /// see at a glance which ones need attention. Paged, because this grows without bound.</summary>
    [HttpGet]
    public async Task<ActionResult<AdminRequestListDto>> List(
        [FromQuery] string? q,
        [FromQuery] string? status,
        [FromQuery] string? requestType,
        [FromQuery] bool? onlyFailures,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        if (!await CanViewAsync(ct)) return Forbid();

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.Requests.AsNoTracking().Include(r => r.Employees).AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            var like = "%" + term + "%";
            query = query.Where(r =>
                EF.Functions.Like(r.RequestNumber, like) ||
                EF.Functions.Like(r.CreatedByDisplayName, like) ||
                r.Employees.Any(e => EF.Functions.Like(e.NameSnapshot, like)));
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<RequestStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(r => r.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(requestType) && Enum.TryParse<RequestType>(requestType, true, out var parsedType))
        {
            query = query.Where(r => r.RequestType == parsedType);
        }

        if (onlyFailures == true)
        {
            query = query.Where(r => _db.RequestTickets.Any(t => t.RequestId == r.RequestId && t.Outcome == TicketOutcome.Failed));
        }

        var total = await query.CountAsync(ct);

        var pageRows = await query
            .OrderByDescending(r => r.RequestId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new
            {
                r.RequestId,
                r.RequestNumber,
                r.RequestType,
                r.Status,
                r.CreatedByDisplayName,
                r.CreatedAt,
                r.SubmittedAt,
                EmployeeNames = r.Employees.Select(e => e.NameSnapshot).ToList()
            })
            .ToListAsync(ct);

        // Ticket counts fetched in ONE query for the whole page rather than per row — this is the
        // most-loaded admin view and a per-row query would make it N+1.
        var ids = pageRows.Select(x => x.RequestId).ToList();
        var ticketStats = await _db.RequestTickets.AsNoTracking()
            .Where(t => ids.Contains(t.RequestId))
            .GroupBy(t => t.RequestId)
            .Select(g => new
            {
                RequestId = g.Key,
                Created = g.Count(t => t.Outcome == TicketOutcome.Created),
                Failed = g.Count(t => t.Outcome == TicketOutcome.Failed)
            })
            .ToDictionaryAsync(x => x.RequestId, ct);

        return Ok(new AdminRequestListDto
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Items = pageRows.Select(r => new AdminRequestSummaryDto
            {
                RequestId = r.RequestId,
                RequestNumber = r.RequestNumber,
                RequestType = r.RequestType.ToString(),
                Status = r.Status.ToString(),
                DemandePar = r.CreatedByDisplayName,
                CreatedAt = r.CreatedAt,
                SubmittedAt = r.SubmittedAt,
                EmployeeNames = r.EmployeeNames,
                TicketsCreated = ticketStats.TryGetValue(r.RequestId, out var created) ? created.Created : 0,
                TicketsFailed = ticketStats.TryGetValue(r.RequestId, out var failed) ? failed.Failed : 0
            }).ToList()
        });
    }

    /// <summary>One request with everything the requester entered, plus every ticket it produced.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<AdminRequestDetailDto>> Detail(int id, CancellationToken ct)
    {
        if (!await CanViewAsync(ct)) return Forbid();

        var request = await _db.Requests.AsNoTracking()
            .Include(r => r.Employees)
            .Include(r => r.OnboardingDetail)
            .Include(r => r.OffboardingDetail)
            .Include(r => r.AccessDetail).ThenInclude(a => a!.Systemes)
            .Include(r => r.AccessDetail).ThenInclude(a => a!.PosHebergement)
            .Include(r => r.EquipmentDetail).ThenInclude(e => e!.Equipements)
            .Include(r => r.ApplicationsDetail).ThenInclude(a => a!.Applications)
            .Include(r => r.ConfidentialComment)
            .Include(r => r.OnboardingConfidentialComment)
            .FirstOrDefaultAsync(r => r.RequestId == id, ct);
        if (request is null) return NotFound();

        var dto = RequestMapper.ToDto(request);

        // The RH comment lives in a physically separate, access-restricted table and is never mapped
        // by default. PRODUCT DECISION 2026-08-27: an app ADMIN may read it here, because the
        // Administration screen exists to review a request in full and a redacted request is not
        // reviewable. A Lecteur may NOT — read-only access to the section is not the same as
        // clearance for HR-confidential content. The pre-existing rule (author of one's own draft,
        // or a member of TRM-RH-ADM) still applies on top, so nobody LOSES access here.
        var isAdmin = await _appUsers.IsAdminAsync(User.GetObjectId(), ct);
        if (isAdmin || _authz.CanReadConfidentialComment(request, User.GetObjectId()))
        {
            dto.CommentairesRH = request.ConfidentialComment?.CommentaireRH ?? request.OnboardingConfidentialComment?.CommentaireRH;
        }

        var employeeNames = request.Employees.ToDictionary(e => e.RequestEmployeeId, e => e.NameSnapshot);
        var tickets = await _tickets.ListForRequestAsync(id, ct);

        return Ok(new AdminRequestDetailDto
        {
            Request = dto,
            RequesterEmail = request.RequesterEmail,
            SubmittedAt = request.SubmittedAt,
            CanRetry = isAdmin,
            Tickets = tickets.Select(t => ToDto(t, employeeNames)).ToList()
        });
    }

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
    /// unconditionally, which on #INT-2026-00053 would have duplicated the two D365 records that had
    /// actually succeeded while fixing the two that failed.</summary>
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
