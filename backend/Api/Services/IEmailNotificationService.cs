namespace TremblantLifecycle.Api.Services;

public interface IEmailNotificationService
{
    /// <summary>Sends to the fixed IT inbox (SendGridOptions.ToAddress) — used for "something
    /// failed, someone needs to look" alerts.</summary>
    Task SendAsync(string subject, string body, CancellationToken ct);

    /// <summary>Sends to an arbitrary list of addresses — used for notifying matched D365 approvers,
    /// where the recipient is a real person, not the IT inbox.</summary>
    Task SendAsync(string subject, string body, IReadOnlyList<string> toAddresses, CancellationToken ct);
}
