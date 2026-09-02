namespace TremblantLifecycle.Api.Models.Entities;

public enum RequestType
{
    Onboarding,
    Reactivation,
    Offboarding,

    /// <summary>A direct D365 access request submitted from the standalone D365AccessRequest app —
    /// NOT part of the onboarding/réactivation/offboarding wizard. Carries no OnboardingDetail,
    /// AccessDetail, etc.; it exists solely to give its D365AccessApproval a Request to hang off
    /// of, matching every other approval's shape. Stored as a string (see AppDbContext), so adding
    /// this value needed no migration.</summary>
    D365AccessOnly
}

public enum RequestStatus
{
    Brouillon,
    Soumise,
    EnTraitement,
    Completee
}

/// <summary>The root request record. One row per submission regardless of type; type-specific data
/// lives in the 1:1 detail tables (OnboardingDetail, OffboardingDetail, etc.).</summary>
public class Request
{
    public int RequestId { get; set; }

    /// <summary>Server-generated (e.g. "INT-2026-00024"), never client-supplied. Backed by a SQL
    /// SEQUENCE per type/year to avoid a race condition from app-side MAX()+1.</summary>
    public string RequestNumber { get; set; } = null!;

    public RequestType RequestType { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.Brouillon;

    /// <summary>Entra ID object id of the submitting manager — the authorization check for
    /// re-reading a draft's own RH comment keys off this.</summary>
    public string CreatedByObjectId { get; set; } = null!;
    public string CreatedByDisplayName { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>The requester's email, captured from AD at SUBMIT time and stored, because it is the
    /// "requester" on every downstream ticket — in Freshdesk it drives the whole reply thread.
    ///
    /// It has to be persisted rather than re-derived: the integrations originally read it from
    /// whoever was calling, which is correct at submit time but wrong on a retry, where the caller
    /// is an administrator rather than the person who made the request. Without this, retrying a
    /// failed ticket would silently raise it under the admin's name.</summary>
    public string? RequesterEmail { get; set; }

    public ICollection<RequestEmployee> Employees { get; set; } = new List<RequestEmployee>();
    public OnboardingDetail? OnboardingDetail { get; set; }
    public OnboardingConfidentialComment? OnboardingConfidentialComment { get; set; }
    public AccessDetail? AccessDetail { get; set; }
    public EquipmentDetail? EquipmentDetail { get; set; }
    public ApplicationsDetail? ApplicationsDetail { get; set; }
    public OffboardingDetail? OffboardingDetail { get; set; }
    public OffboardingConfidentialComment? ConfidentialComment { get; set; }
    public D365AccessApproval? D365AccessApproval { get; set; }
    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
}
