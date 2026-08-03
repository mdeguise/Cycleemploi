using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace TremblantLifecycle.Api.Services;

public class SendGridEmailNotificationService : IEmailNotificationService
{
    private readonly SendGridOptions _options;
    private readonly ILogger<SendGridEmailNotificationService> _logger;

    public SendGridEmailNotificationService(IOptions<SendGridOptions> options, ILogger<SendGridEmailNotificationService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(string subject, string body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("SendGrid API key not configured — skipping failure-notification email. Subject: {Subject}\n{Body}", subject, body);
            return;
        }

        var client = new SendGridClient(_options.ApiKey);
        var message = MailHelper.CreateSingleEmail(
            new EmailAddress(_options.FromAddress),
            new EmailAddress(_options.ToAddress),
            subject,
            body,
            htmlContent: null);

        var response = await client.SendEmailAsync(message, ct);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Body.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"SendGrid returned {(int)response.StatusCode}: {responseBody}");
        }
    }
}
