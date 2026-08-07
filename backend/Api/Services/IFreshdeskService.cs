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

    /// <summary>Creates a Freshdesk "child" ticket (Freshdesk's Parent-child ticketing feature —
    /// parent_id, association_type 1/2) in the given group, linked to the already-created main
    /// ticket. Used to fan a submission out to other departments (e.g. payroll, IT provisioning)
    /// that need a subset of the request's details without seeing the full RH ticket. Throws on any
    /// failure — same best-effort contract as CreateTicketAsync.</summary>
    Task<long> CreateChildTicketAsync(Request request, long parentTicketId, string requesterEmail, long groupId, bool includeAllJobCodes, CancellationToken ct);
}

public class FreshdeskTicketException(string message) : Exception(message);
