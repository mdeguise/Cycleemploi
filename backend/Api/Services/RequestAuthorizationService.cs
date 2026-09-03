using Microsoft.Extensions.Options;
using TremblantLifecycle.Api.Models.Entities;

namespace TremblantLifecycle.Api.Services;

public class HrGroupOptions
{
    public string TrmRhAdmGroupName { get; set; } = null!;

    /// <summary>AD security group that gates the Écarts/Réconciliation (discrepancy) view — distinct
    /// from the RH group, which gates the confidential offboarding comment. Set in appsettings.json.</summary>
    public string CycleEmploiD365AdminGroupName { get; set; } = null!;
}

/// <summary>The single source of truth for the RH-comment access rule: readable ONLY by HR
/// (TRM-RH-ADM) — not even the original author, since a Request only ever exists already-submitted
/// (no partial-save/draft state to grant a pre-submission read-back window). This is the only code
/// path that should gate reads of OffboardingConfidentialComment/OnboardingConfidentialComment —
/// controllers must call this before including the field in any response, never infer access from
/// the UI alone.</summary>
public class RequestAuthorizationService
{
    private readonly IAdDirectoryService _ad;
    private readonly string _hrGroupName;

    public RequestAuthorizationService(IAdDirectoryService ad, IOptions<HrGroupOptions> hrGroupOptions)
    {
        _ad = ad;
        _hrGroupName = hrGroupOptions.Value.TrmRhAdmGroupName;
    }

    /// <summary>True if the currently authenticated caller (identified by their Windows account
    /// name) may read the confidential RH comment on the given request.</summary>
    public bool CanReadConfidentialComment(Request request, string callerAccountName) =>
        _ad.IsUserInGroup(callerAccountName, _hrGroupName);
}
