namespace TremblantLifecycle.Api.Services;

public interface IEmailNotificationService
{
    Task SendAsync(string subject, string body, CancellationToken ct);
}
