namespace TremblantLifecycle.Api.Services;

/// <summary>Checks Entra ID group membership for the current caller via Microsoft Graph, rather
/// than trusting a "groups" claim on the token — see the plan's auth-flow notes: a groups claim can
/// silently overflow past Entra's ~200-group limit into an "overage" indirection that's easy to
/// mishandle, so a server-side Graph check keeps the authorization decision authoritative.</summary>
public interface IGraphGroupService
{
    /// <summary>True if the currently authenticated user (via the incoming request's on-behalf-of
    /// token) is a member of the given Entra group id.</summary>
    Task<bool> IsCallerInGroupAsync(string groupObjectId, CancellationToken ct = default);
}
