namespace TremblantLifecycle.Api.Services;

/// <summary>Used only to notify IT support when a downstream ticket-system integration (Freshdesk,
/// later TDX) fails after a request was already successfully submitted — see
/// RequestsController.Submit. Host left empty means "not configured yet"; the email service treats
/// that as a no-op (logs a warning) rather than throwing, since a missing SMTP relay shouldn't be
/// able to break anything beyond the notification itself.</summary>
public class SmtpOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 25;
    public bool EnableSsl { get; set; }
    public string? User { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = "cycleemploi@tremblant.ca";
    public string ToAddress { get; set; } = "supportinformatique@tremblant.ca";
}
