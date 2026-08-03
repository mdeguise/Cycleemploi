namespace TremblantLifecycle.Api.Services;

/// <summary>Triggers the existing Power Automate flow that creates D365 F&amp;O Enterprise Asset
/// Management work orders (AssetMaintenanceRequests) for badge/alarm activation — via its "When an
/// HTTP request is received" trigger, instead of Cycle Emploi calling D365 directly. Avoids needing
/// a dedicated Entra ID app registration + D365 security role just for this; the flow already has
/// the D365 connection configured. The trigger URL Power Automate generates carries its own SAS
/// signature (the "sig=" query parameter) as authentication — treat it exactly like an API key:
/// WebhookUrl deliberately left empty here, the real value goes in appsettings.Production.json
/// (gitignored, never committed).</summary>
public class PowerAutomateOptions
{
    public string BadgeRequestWebhookUrl { get; set; } = "";
}
