using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace TremblantLifecycle.Api.Services;

public class SmtpEmailNotificationService : IEmailNotificationService
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailNotificationService> _logger;

    public SmtpEmailNotificationService(IOptions<SmtpOptions> options, ILogger<SmtpEmailNotificationService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(string subject, string body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.Host))
        {
            _logger.LogWarning("SMTP relay not configured — skipping failure-notification email. Subject: {Subject}\n{Body}", subject, body);
            return;
        }

        using var client = new SmtpClient(_options.Host, _options.Port) { EnableSsl = _options.EnableSsl };
        if (!string.IsNullOrWhiteSpace(_options.User))
        {
            client.Credentials = new NetworkCredential(_options.User, _options.Password);
        }

        using var message = new MailMessage(_options.FromAddress, _options.ToAddress, subject, body);
        await client.SendMailAsync(message, ct);
    }
}
