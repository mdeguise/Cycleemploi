namespace TremblantLifecycle.Api.Services;

/// <summary>Used only to notify IT support when a downstream ticket-system integration (Freshdesk,
/// later TDX) fails after a request was already successfully submitted — see
/// RequestsController.Submit. ApiKey left empty means "not configured yet"; the email service treats
/// that as a no-op (logs a warning) rather than throwing, since a missing SendGrid key shouldn't be
/// able to break anything beyond the notification itself. The real key lives only in
/// appsettings.Production.json on the server (gitignored, never committed).</summary>
public class SendGridOptions
{
    public string ApiKey { get; set; } = "";
    public string FromAddress { get; set; } = "Trm_FlowSvc@tremblant.ca";
    public string ToAddress { get; set; } = "supportinformatique@tremblant.ca";
}
