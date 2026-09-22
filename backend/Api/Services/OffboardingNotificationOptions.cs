namespace TremblantLifecycle.Api.Services;

/// <summary>Fixed departmental distribution address that must be copied on every termination/layoff
/// request, alongside HR's own RH Général Freshdesk ticket — see
/// TicketOrchestrationService.TrySendAvisArretDeTravailEmailAsync.</summary>
public class OffboardingNotificationOptions
{
    public string AvisArretDeTravailAddress { get; set; } = "AVISARRETDETRAVAIL@tremblant.ca";
}
