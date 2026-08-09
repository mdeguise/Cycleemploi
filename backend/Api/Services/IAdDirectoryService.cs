namespace TremblantLifecycle.Api.Services;

public record AdUserInfo(string? DisplayName, string? Email);

/// <summary>A Tremblant AD account (extensionAttribute2 = "T"), including disabled ones.
/// <paramref name="Sam"/> is the sAMAccountName (join key to Dynaway's User_ login);
/// <paramref name="Cn"/> is the display name incl. the "(T)" suffix (join key to the D365 user
/// name); <paramref name="Enabled"/> is false when the account is disabled; <paramref name="EmployeeId"/>
/// is the Workday EmployeeId when set on the account.</summary>
public record AdAccount(string Sam, string? Cn, bool Enabled, string? EmployeeId);

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

    /// <summary>All Tremblant AD accounts (extensionAttribute2 = "T"), enabled and disabled, in a
    /// single LDAP query. Used by the reconciliation view to resolve account status for Dynaway
    /// logins (by Sam) and D365 users (by Cn) without a per-user round trip.</summary>
    IReadOnlyList<AdAccount> GetTremblantAccounts();
}
