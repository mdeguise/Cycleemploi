using System.Security.Claims;

namespace TremblantLifecycle.Api.Services;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Entra ID's stable per-user object id — "oid" claim, falling back to
    /// NameIdentifier for local/dev token setups that don't populate oid.</summary>
    public static string GetObjectId(this ClaimsPrincipal user) =>
        user.FindFirstValue("oid")
        ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("No object id claim on the authenticated user.");

    public static string GetDisplayName(this ClaimsPrincipal user) =>
        user.FindFirstValue("name")
        ?? user.FindFirstValue(ClaimTypes.Name)
        ?? "Unknown";

    public static string? GetEmail(this ClaimsPrincipal user) =>
        user.FindFirstValue("preferred_username")
        ?? user.FindFirstValue(ClaimTypes.Email);
}
