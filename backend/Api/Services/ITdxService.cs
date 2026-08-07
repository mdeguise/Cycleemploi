using TremblantLifecycle.Api.Models.Entities;

namespace TremblantLifecycle.Api.Services;

/// <summary>Creates a ticket in TeamDynamix (TDX) for a submitted request, in the "OneIT" app's
/// "Quick Incident" form, routed to the IT Operations group. Third downstream system after
/// Freshdesk and D365, same best-effort contract — the caller decides what "best-effort" means.</summary>
public interface ITdxService
{
    /// <returns>The created TDX ticket's numeric ID.</returns>
    Task<int> CreateTicketAsync(Request request, RequestEmployee employee, string requesterName, string requesterEmail, CancellationToken ct);
}

public class TdxTicketException(string message) : Exception(message);
