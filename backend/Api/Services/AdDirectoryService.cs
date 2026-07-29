using System.DirectoryServices.AccountManagement;
using System.Runtime.Versioning;

namespace TremblantLifecycle.Api.Services;

[SupportedOSPlatform("windows")]
public class AdDirectoryService : IAdDirectoryService
{
    public bool IsUserInGroup(string samAccountName, string groupName)
    {
        using var ctx = new PrincipalContext(ContextType.Domain);
        using var user = UserPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, NormalizeSam(samAccountName));
        if (user is null) return false;

        using var group = GroupPrincipal.FindByIdentity(ctx, groupName);
        return group is not null && user.IsMemberOf(group);
    }

    public AdUserInfo GetUserInfo(string samAccountName)
    {
        using var ctx = new PrincipalContext(ContextType.Domain);
        using var user = UserPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, NormalizeSam(samAccountName));
        return new AdUserInfo(user?.DisplayName, user?.EmailAddress);
    }

    // Windows identity Name comes back as "DOMAIN\samaccountname" — AD lookups need the bare name.
    private static string NormalizeSam(string identityName) =>
        identityName.Contains('\\') ? identityName.Split('\\')[^1] : identityName;
}
