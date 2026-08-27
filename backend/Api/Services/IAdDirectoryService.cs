namespace TremblantLifecycle.Api.Services;

public record AdUserInfo(string? DisplayName, string? Email);

/// <summary>An AD account. <paramref name="Sam"/> is the sAMAccountName (join key to Dynaway's
/// User_ login); <paramref name="Cn"/> is the display name incl. the "(T)" suffix; <paramref name="Enabled"/>
/// is false when disabled; <paramref name="EmployeeId"/> is the Workday EmployeeId when set on the
/// account; <paramref name="Email"/> is the mail attribute — matches Workday's Work_Email, a reliable
/// fallback link to a Workday record when the EmployeeId join is missing.</summary>
public record AdAccount(string Sam, string? Cn, bool Enabled, string? EmployeeId, string? Email);

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
    /// logins (by Sam) without a per-user round trip.</summary>
    IReadOnlyList<AdAccount> GetTremblantAccounts();

    /// <summary>AD accounts whose employeeID attribute is one of the given values, across the whole
    /// directory (NOT limited to Tremblant/ext2=T — a person can hold Tremblant D365 roles while
    /// their AD account is tagged another resort). employeeID equals the Workday EmployeeId, so this
    /// is the reliable join from a D365 user (once linked to Workday) to their AD account, immune to
    /// display-name typos/renames. Batched into chunked LDAP OR-filters.</summary>
    IReadOnlyList<AdAccount> GetAccountsByEmployeeId(IEnumerable<string> employeeIds);

    /// <summary>Prefix-searches enabled accounts by sAMAccountName / display name / first / last /
    /// mail, for the "add a user" people picker. Searches the domain named by Ad:SearchDomain when
    /// set, otherwise whatever domain the app server itself is joined to — that distinction matters
    /// while vm-trm-live is still iDirectory-joined but access is granted to ENTERPRISE accounts.
    /// The query is LDAP-escaped before use.</summary>
    IReadOnlyList<AdAccount> SearchAccounts(string query, int limit);
}
