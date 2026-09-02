namespace TremblantLifecycle.Api.Services;

/// <summary>Config for building a working link into a notification email — specifically the
/// standalone D365Approvals app's own origin (NOT Cycle Emploi's), since that's where the email
/// sends a matched D365Approver to review and complete a pending approval. There is nowhere else
/// in the backend that knows what origin that app is being served from.</summary>
public class AppOptions
{
    public string BaseUrl { get; set; } = "";
}
