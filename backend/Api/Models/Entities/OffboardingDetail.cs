namespace TremblantLifecycle.Api.Models.Entities;

/// <summary>1:1 with Request. Only populated for Offboarding requests. CommentairesRH is
/// DELIBERATELY NOT here — it lives only in OffboardingConfidentialComment, so it can never
/// accidentally get joined into the general "get request" query. See
/// OffboardingConfidentialComment and RequestAuthorizationService for the actual access-control
/// enforcement.</summary>
public class OffboardingDetail
{
    public int RequestId { get; set; }
    public Request Request { get; set; } = null!;

    public DateOnly DerniereJournee { get; set; }
    public string IndemniteVacances { get; set; } = null!;
    public string RaisonArret { get; set; } = null!;
    public string DetailsRaison { get; set; } = null!;
    public string Reembaucheriez { get; set; } = null!;
    public string? CommentairesIT { get; set; }
    public string? CommentairesParkingAcces { get; set; }
    public string? CommentairesRedingote { get; set; }
}
