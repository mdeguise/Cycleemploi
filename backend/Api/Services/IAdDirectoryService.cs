namespace TremblantLifecycle.Api.Services;

public record AdUserInfo(string? DisplayName, string? Email);

/// <summary>Checks AD group membership and looks up basic profile info for the current caller via
/// System.DirectoryServices.AccountManagement (LDAP against the domain the app server is joined
/// to), rather than an Entra ID app registration + Microsoft Graph — the app trusts the same
/// Windows identity already authenticated by Negotiate.</summary>
public interface IAdDirectoryService
{
    /// <summary>True if the given account (SAM account name, e.g. "jsmith") is a member of the
    /// given AD security group (by name, e.g. "TRM-RH-ADM").</summary>
    bool IsUserInGroup(string samAccountName, string groupName);

    /// <summary>Display name and email for the given account, or nulls if the account can't be
    /// resolved against AD (falls back to the raw Windows account name at the call site).</summary>
    AdUserInfo GetUserInfo(string samAccountName);
}
