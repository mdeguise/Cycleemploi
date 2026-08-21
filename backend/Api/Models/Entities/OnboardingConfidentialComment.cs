namespace TremblantLifecycle.Api.Models.Entities;

/// <summary>Physically separate table holding ONLY the confidential RH comment for
/// Onboarding/Reactivation requests — mirrors OffboardingConfidentialComment's separation, which is
/// the foundation of the access-control guarantee, not just a convenience. Never joined into the
/// general request query; only RequestAuthorizationService's single narrow repository method touches
/// this table. Read access: caller is in the TRM-RH-ADM Entra group, OR caller is the request's
/// original author AND the request is still Status == Brouillon. Write access is normal — the author
/// fills this in as part of ordinary wizard data entry regardless of who can read it back later.</summary>
public class OnboardingConfidentialComment
{
    public int RequestId { get; set; }
    public Request Request { get; set; } = null!;

    public string? CommentaireRH { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? UpdatedByObjectId { get; set; }
}
