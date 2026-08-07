using TremblantLifecycle.Api.Models.Entities;

namespace TremblantLifecycle.Api.Services;

/// <summary>Creates a D365 F&amp;O Enterprise Asset Management work order (AssetMaintenanceRequests)
/// when a request includes "Badge d'accès aux édifices" — see RequestsController.Submit. Replaces
/// the previous Freshservice-triggered Power Automate flow; Cycle Emploi's own submission is now the
/// trigger, and the data comes directly from the wizard instead of parsed ticket custom fields.</summary>
public interface IDynamicsEamService
{
    /// <param name="freshdeskTicketId">The id of the Freshdesk ticket created for this same
    /// submission (if that succeeded) — included in the webhook payload for cross-referencing.
    /// Null if the Freshdesk ticket creation failed or hasn't happened.</param>
    /// <returns>The jobcode of the created AssetMaintenanceRequests record.</returns>
    Task<string> CreateBadgeRequestAsync(Request request, RequestEmployee employee, long? freshdeskTicketId, CancellationToken ct);
}

public class DynamicsEamException(string message) : Exception(message);
