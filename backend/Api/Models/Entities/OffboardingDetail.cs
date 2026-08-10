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

    public DateOnly? DerniereJournee { get; set; }
    public string? IndemniteVacances { get; set; }
    public string? RaisonArret { get; set; }
    public string? DetailsRaison { get; set; }
    public string? Reembaucheriez { get; set; }
    public string? CommentairesIT { get; set; }
    public string? CommentairesStationnement { get; set; }
    public string? CommentairesPuceAcces { get; set; }
    public string? CommentairesRedingote { get; set; }

    /// <summary>Shown only when RaisonArret is "Mise à pied temporaire (manque de travail)" — asks
    /// whether the return date is known before optionally capturing it.</summary>
    public string? DateRetourConnue { get; set; }
    public DateOnly? DateRetourTravail { get; set; }

    /// <summary>Shown only when RaisonArret is "Démission volontaire".</summary>
    public string? PreavisRecu { get; set; }

    /// <summary>Shown only when Reembaucheriez is "Non".</summary>
    public string? MotifNonAdmissibilite { get; set; }
}
