using TremblantLifecycle.Api.Models.Entities;

namespace TremblantLifecycle.Api.Services;

/// <summary>Creates tickets in TeamDynamix (TDX). CreateTicketAsync is the "OneIT" app's "Quick
/// Incident" form, routed to IT Operations — third downstream system after Freshdesk and D365, same
/// best-effort contract — the caller decides what "best-effort" means. CreateD365AccessTicketAsync
/// is the separate "D365 - Access" form (FormID 10799).</summary>
public interface ITdxService
{
    /// <returns>The created TDX ticket's numeric ID.</returns>
    Task<int> CreateTicketAsync(Request request, RequestEmployee employee, string requesterName, string requesterEmail, CancellationToken ct);

    /// <returns>The created TDX ticket's numeric ID.</returns>
    Task<int> CreateD365AccessTicketAsync(D365AccessTicketInput input, CancellationToken ct);

    /// <returns>The TDX person UID for the given email, or null if the lookup fails for any reason
    /// (no match, TDX unreachable, auth failure). Used to personalize the "Besoin d'aide?" link —
    /// a non-critical UI convenience, so a failed lookup should silently degrade rather than block
    /// the caller the way the ticket-creation methods' exceptions are meant to.</returns>
    Task<string?> TryLookupPersonUidAsync(string email, CancellationToken ct);

    /// <summary>Creates a ticket for the in-app French "Besoin d'aide?" form — same FormID/
    /// AccountID/ResponsibleGroupID as CreateTicketAsync ("Quick Incident"), with Subject/Category/
    /// Priority fixed to identify these as coming from this app, and the free-text description
    /// supplied by the user.</summary>
    /// <returns>The created TDX ticket's numeric ID.</returns>
    Task<int> CreateHelpTicketAsync(string requesterName, string requesterEmail, string description, CancellationToken ct);

    /// <summary>Current state of an existing TDX ticket. NEVER throws — called while rendering a
    /// list, where one unreachable ticket must not fail the page. Failure returns Unknown.</summary>
    Task<LiveTicketStatus> GetTicketStatusAsync(int ticketId, CancellationToken ct);
}

/// <summary>Everything needed to fill out the "D365 - Access" TDX form (FormID 10799) for one
/// employee — resolved by the caller (job code lookups, Workday queries) so this service stays a
/// pure TDX API wrapper, same pattern as CreateTicketAsync's parameters.</summary>
public record D365AccessTicketInput(
    string RequesterName,
    string RequesterEmail,
    string EmployeeName,
    string EmployeeEmail,
    string? JobTitle,
    string LegalEntity,
    string DepartmentNumber,
    bool LevyEmployee,
    string? ManagerName,
    DateOnly? StartDate,
    IReadOnlyList<string> Roles,
    decimal ApprovalLimit,
    string? ApAccessDetails,
    string? AdditionalLegalEntities
);

public class TdxTicketException(string message) : Exception(message);
