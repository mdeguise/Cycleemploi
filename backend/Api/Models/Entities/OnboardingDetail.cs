namespace TremblantLifecycle.Api.Models.Entities;

/// <summary>1:1 with Request. Only populated for Onboarding/Reactivation requests. CommentairesRH is
/// DELIBERATELY NOT here — it lives only in OnboardingConfidentialComment, so it can never
/// accidentally get joined into the general "get request" query. See OnboardingConfidentialComment
/// and RequestAuthorizationService for the actual access-control enforcement.</summary>
public class OnboardingDetail
{
    public int RequestId { get; set; }
    public Request Request { get; set; } = null!;

    public DateOnly? DateEntreePrevue { get; set; }
    public string? RegleDePaye { get; set; }
    public string? RegleDePayeCommentaire { get; set; }
    public string? CommentairesIT { get; set; }
    public string? CommentairesStationnement { get; set; }
    public string? CommentairesPuceAcces { get; set; }
    public string? CommentairesRedingote { get; set; }
}
