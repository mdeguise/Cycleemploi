using System.Security.Claims;

namespace TremblantLifecycle.Api.Services;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Windows Negotiate auth's Identity.Name — "DOMAIN\samaccountname". Stable per
    /// account, used as the caller identifier everywhere an Entra "oid" was used before.</summary>
    public static string GetObjectId(this ClaimsPrincipal user) =>
        user.Identity?.Name ?? throw new InvalidOperationException("No authenticated Windows identity.");

    /// <summary>Bare SAM account name (no "DOMAIN\" prefix) for AD lookups.</summary>
    public static string GetSamAccountName(this ClaimsPrincipal user)
    {
        var name = user.GetObjectId();
        var slash = name.IndexOf('\\');
        return slash >= 0 ? name[(slash + 1)..] : name;
    }
}
