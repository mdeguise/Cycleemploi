namespace TremblantLifecycle.Api.Services;

/// <summary>Direct D365 Finance &amp; Operations OData integration — replaces the Power Automate flow
/// that used to create Enterprise Asset Management "AssetMaintenanceRequests" work orders for badge
/// activation. Auth is OAuth2 client-credentials against Entra ID; the app registration's service
/// principal must be provisioned as a D365 F&amp;O user with a security role granting write access to
/// AssetMaintenanceRequests (System administration &gt; Users &gt; New &gt; via Azure AD Application).
/// ClientSecret left empty means "not configured yet" — treated as a no-op, same pattern as
/// Freshdesk/SendGrid. Real values go in appsettings.Production.json (gitignored).</summary>
public class DynamicsOptions
{
    public string BaseUrl { get; set; } = "https://alterra.operations.dynamics.com";
    public string TenantId { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string DataAreaId { get; set; } = "6201";
    public string FunctionalLocationId { get; set; } = "BF-SEC-GEN";
    public string RequestTypeId { get; set; } = "GENERAL";
}
