namespace TremblantLifecycle.Api.Models.Entities;

/// <summary>1:1 with Request. Only populated for Onboarding/Reactivation requests.</summary>
public class OnboardingDetail
{
    public int RequestId { get; set; }
    public Request Request { get; set; } = null!;

    public DateOnly DateEntreePrevue { get; set; }
    public string RegleDePaye { get; set; } = null!;
    public string? RegleDePayeCommentaire { get; set; }
}
