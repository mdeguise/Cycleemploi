using System.DirectoryServices;
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

    public IReadOnlyList<AdAccount> GetTremblantAccounts()
    {
        var results = new List<AdAccount>();
        using var root = new DirectoryEntry();
        using var searcher = new DirectorySearcher(root)
        {
            // Tremblant users only, enabled and disabled alike; extensionAttribute2 = "T" is the
            // resort tag confirmed on the live directory.
            Filter = "(&(objectCategory=person)(objectClass=user)(extensionAttribute2=T))",
            PageSize = 1000,
        };
        searcher.PropertiesToLoad.AddRange(new[] { "sAMAccountName", "cn", "userAccountControl", "employeeID", "mail" });

        using var found = searcher.FindAll();
        foreach (SearchResult r in found)
        {
            var sam = GetProp(r, "sAMAccountName");
            if (string.IsNullOrEmpty(sam)) continue;

            var uac = r.Properties["userAccountControl"].Count > 0
                ? Convert.ToInt32(r.Properties["userAccountControl"][0])
                : 0;
            var enabled = (uac & 0x2) == 0; // 0x2 = ACCOUNTDISABLE

            results.Add(new AdAccount(sam, GetProp(r, "cn"), enabled, GetProp(r, "employeeID"), GetProp(r, "mail")));
        }
        return results;
    }

    public IReadOnlyList<AdAccount> GetAccountsByEmployeeId(IEnumerable<string> employeeIds)
    {
        var ids = employeeIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var results = new List<AdAccount>();
        if (ids.Count == 0) return results;

        using var root = new DirectoryEntry();
        // Chunk the OR-filter so it stays well under LDAP filter-size limits.
        foreach (var chunk in ids.Chunk(100))
        {
            var or = string.Concat(chunk.Select(id => $"(employeeID={EscapeLdap(id)})"));
            using var searcher = new DirectorySearcher(root)
            {
                Filter = $"(&(objectCategory=person)(objectClass=user)(|{or}))",
                PageSize = 1000,
            };
            searcher.PropertiesToLoad.AddRange(new[] { "sAMAccountName", "cn", "userAccountControl", "employeeID", "mail" });

            using var found = searcher.FindAll();
            foreach (SearchResult r in found)
            {
                var sam = GetProp(r, "sAMAccountName");
                if (string.IsNullOrEmpty(sam)) continue;
                var uac = r.Properties["userAccountControl"].Count > 0
                    ? Convert.ToInt32(r.Properties["userAccountControl"][0])
                    : 0;
                results.Add(new AdAccount(sam, GetProp(r, "cn"), (uac & 0x2) == 0, GetProp(r, "employeeID"), GetProp(r, "mail")));
            }
        }
        return results;
    }

    // RFC 4515 escaping for values interpolated into an LDAP filter.
    private static string EscapeLdap(string v) => v
        .Replace("\\", "\\5c").Replace("*", "\\2a").Replace("(", "\\28").Replace(")", "\\29").Replace("\0", "\\00");

    private static string? GetProp(SearchResult r, string name) =>
        r.Properties[name].Count > 0 ? r.Properties[name][0]?.ToString() : null;

    // Windows identity Name comes back as "DOMAIN\samaccountname" — AD lookups need the bare name.
    private static string NormalizeSam(string identityName) =>
        identityName.Contains('\\') ? identityName.Split('\\')[^1] : identityName;
}
