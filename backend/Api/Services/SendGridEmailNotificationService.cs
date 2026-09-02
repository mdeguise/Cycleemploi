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

    public Task SendAsync(string subject, string body, CancellationToken ct) =>
        SendAsync(subject, body, [_options.ToAddress], ct);

    public async Task SendAsync(string subject, string body, IReadOnlyList<string> toAddresses, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("SendGrid API key not configured — skipping email to {ToAddresses}. Subject: {Subject}\n{Body}", string.Join(", ", toAddresses), subject, body);
            return;
        }
        if (toAddresses.Count == 0)
        {
            _logger.LogWarning("No recipients given — skipping email. Subject: {Subject}", subject);
            return;
        }

        var client = new SendGridClient(_options.ApiKey);
        var message = MailHelper.CreateSingleEmailToMultipleRecipients(
            new EmailAddress(_options.FromAddress),
            toAddresses.Select(a => new EmailAddress(a)).ToList(),
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
