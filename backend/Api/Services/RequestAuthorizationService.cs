using Microsoft.Extensions.Options;
using TremblantLifecycle.Api.Models.Entities;

namespace TremblantLifecycle.Api.Services;

public class HrGroupOptions
{
    public string TrmRhAdmGroupId { get; set; } = null!;
}

/// <summary>The single source of truth for the RH-comment access rule described in the plan:
/// readable ONLY by HR (TRM-RH-ADM), OR by the original submitting manager while the request is
/// still a draft. This is the only code path that should gate reads of
/// OffboardingConfidentialComment — controllers must call this before including the field in any
/// response, never infer access from the UI alone.</summary>
public class RequestAuthorizationService
{
    private readonly IGraphGroupService _graphGroupService;
    private readonly string _hrGroupId;

    public RequestAuthorizationService(IGraphGroupService graphGroupService, IOptions<HrGroupOptions> hrGroupOptions)
    {
        _graphGroupService = graphGroupService;
        _hrGroupId = hrGroupOptions.Value.TrmRhAdmGroupId;
    }

    /// <summary>True if the currently authenticated caller (identified by callerObjectId) may read
    /// the confidential RH comment on the given request.</summary>
    public async Task<bool> CanReadConfidentialCommentAsync(Request request, string callerObjectId, CancellationToken ct = default)
    {
        var isAuthorDraft = request.CreatedByObjectId == callerObjectId && request.Status == RequestStatus.Brouillon;
        if (isAuthorDraft)
        {
            return true;
        }

        return await _graphGroupService.IsCallerInGroupAsync(_hrGroupId, ct);
    }
}
