using TremblantLifecycle.Api.Models.Entities;

namespace TremblantLifecycle.Api.Services;

public interface IFreshdeskService
{
    /// <summary>Creates a Freshdesk ticket for a just-submitted request, returning the created
    /// ticket's numeric id (e.g. so it can be included in the D365 EAM webhook payload). Throws on
    /// any failure (network, non-2xx response) — the caller (RequestsController.Submit) is
    /// responsible for deciding what "best-effort" means (catch, notify, don't block the
    /// submission).</summary>
    Task<long> CreateTicketAsync(Request request, string requesterEmail, CancellationToken ct);

    /// <summary>Creates an independent Freshdesk ticket (no parent_id — a standalone ticket, not
    /// Freshdesk's Parent-child ticketing feature) in "RH - Horaires" (FreshdeskOptions.
    /// HorairesGroupId), including the employee's full job-code history — the payroll/scheduling
    /// department's own fan-out of every submission. Correlated with the other fanned-out tickets
    /// only by sharing the same subject text and request number, same as the main ticket. Throws on
    /// any failure — same best-effort contract as CreateTicketAsync.</summary>
    Task<long> CreateHorairesTicketAsync(Request request, string requesterEmail, CancellationToken ct);

    /// <summary>Same fan-out pattern as CreateHorairesTicketAsync, to "RH - Redingote"
    /// (FreshdeskOptions.RedingoteGroupId) — the uniforms/équipement department.</summary>
    Task<long> CreateRedingoteTicketAsync(Request request, string requesterEmail, CancellationToken ct);

    /// <summary>Same fan-out pattern as CreateHorairesTicketAsync, to "SAC - ISAC"
    /// (FreshdeskOptions.StationnementGroupId) — the parking department.</summary>
    Task<long> CreateStationnementTicketAsync(Request request, string requesterEmail, CancellationToken ct);

    /// <summary>Current state of an existing ticket. Unlike the Create methods this NEVER throws —
    /// it is called while rendering a list, where one unreachable ticket must not fail the whole
    /// page. A failure returns Unknown, which the UI shows as such rather than guessing.</summary>
    Task<LiveTicketStatus> GetTicketStatusAsync(long ticketId, CancellationToken ct);
}

public class FreshdeskTicketException(string message) : Exception(message);
