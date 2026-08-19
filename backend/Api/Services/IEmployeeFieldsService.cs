using TremblantLifecycle.Api.Models.Entities;

namespace TremblantLifecycle.Api.Services;

/// <summary>Resolves the Employee-category field values (see TicketTemplateField) for one employee
/// on a request — combines the request's own snapshot (name/poste/département/etc., captured at
/// wizard time) with a live lookup of the Workday-only fields (hire date, cost center, seniority
/// date, active status, leave type, ...) that aren't captured anywhere else. Shared between
/// FreshdeskService and TdxService so the live Workday query logic isn't duplicated.</summary>
public interface IEmployeeFieldsService
{
    Task<Dictionary<string, string?>> ResolveAsync(RequestEmployee employee, CancellationToken ct);
}
