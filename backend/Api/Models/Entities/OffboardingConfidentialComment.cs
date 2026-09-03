namespace TremblantLifecycle.Api.Models.Entities;

/// <summary>Physically separate table holding ONLY the confidential RH comment — this separation
/// is the foundation of the access-control guarantee, not just a convenience. Never joined into the
/// general request query; only RequestAuthorizationService's single narrow repository method touches
/// this table. Read access: caller is in the TRM-RH-ADM Entra group — nobody else, not even the
/// original author, since a Request row only ever exists already-submitted (no partial-save state
/// to grant the author a pre-submission read-back window). Write access is normal — the author
/// fills this in as part of ordinary wizard data entry regardless of who can read it back later.</summary>
public class OffboardingConfidentialComment
{
    public int RequestId { get; set; }
    public Request Request { get; set; } = null!;

    public string? CommentaireRH { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? UpdatedByObjectId { get; set; }
}
