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
}
