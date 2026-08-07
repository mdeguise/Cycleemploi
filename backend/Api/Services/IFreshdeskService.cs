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
}

public class FreshdeskTicketException(string message) : Exception(message);
