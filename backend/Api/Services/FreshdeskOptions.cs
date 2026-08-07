namespace TremblantLifecycle.Api.Services;

/// <summary>ApiKey is deliberately left empty here — the real value lives in
/// appsettings.Production.json on the server only, never committed to source control. See
/// CONTRIBUTING.md.</summary>
public class FreshdeskOptions
{
    public string Subdomain { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public long GroupId { get; set; }
    public long EmailConfigId { get; set; }
    public string TicketType { get; set; } = "";

    /// <summary>Child ticket group (Freshdesk "Parent-child ticketing" — parent_id on create),
    /// created alongside the main ticket on every submission. Includes all job codes associated to
    /// the employee(s), unlike ChildGroupIdWithoutJobCodes.</summary>
    public long ChildGroupIdWithJobCodes { get; set; }

    public long ChildGroupIdWithoutJobCodes { get; set; }
}
