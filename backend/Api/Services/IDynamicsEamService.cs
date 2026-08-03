using TremblantLifecycle.Api.Models.Entities;

namespace TremblantLifecycle.Api.Services;

/// <summary>Creates a D365 F&amp;O Enterprise Asset Management work order (AssetMaintenanceRequests)
/// when a request includes "Badge d'accès aux édifices" — see RequestsController.Submit. Replaces
/// the previous Freshservice-triggered Power Automate flow; Cycle Emploi's own submission is now the
/// trigger, and the data comes directly from the wizard instead of parsed ticket custom fields.</summary>
public interface IDynamicsEamService
{
    /// <returns>The RequestId of the created AssetMaintenanceRequests record (e.g. "WREF0000012346").</returns>
    Task<string> CreateBadgeRequestAsync(Request request, RequestEmployee employee, CancellationToken ct);
}

public class DynamicsEamException(string message) : Exception(message);
