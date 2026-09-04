using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Runtime.Versioning;

using Microsoft.Extensions.Options;

namespace TremblantLifecycle.Api.Services;

/// <summary>Directory settings. <see cref="SearchDomain"/> is empty by default, meaning "use the
/// domain this server is joined to". Set it (e.g. "enterprise.ad") to search a different domain
/// across the forest trust — needed because vm-trm-live is still joined to iDirectory.itw while
/// access is being granted to ENTERPRISE.AD accounts.</summary>
public class AdOptions
{
    public string SearchDomain { get; set; } = "";
}

[SupportedOSPlatform("windows")]
public class AdDirectoryService : IAdDirectoryService
{
    private readonly AdOptions _options;

    public AdDirectoryService(IOptions<AdOptions> options)
    {
        _options = options.Value;
    }

    // vm-trm-live is still joined to iDirectory during the migration, so a default bind only finds
    // iDirectory accounts. Also search ENTERPRISE.AD (where migrated users now live) and merge.
    private const string MigrationDomain = "enterprise.ad";

    /// <summary>The roots to search, in order: the machine's own domain first, then the migration
    /// domain (AdOptions.SearchDomain override, else enterprise.ad).</summary>
    private IEnumerable<DirectoryEntry> SearchRoots()
    {
        yield return new DirectoryEntry();
        var extra = string.IsNullOrWhiteSpace(_options.SearchDomain) ? MigrationDomain : _options.SearchDomain.Trim();
        yield return new DirectoryEntry($"LDAP://{extra}");
    }

    public IReadOnlyList<AdAccount> SearchAccounts(string query, int limit)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];

        var q = EscapeLdap(query.Trim());
        var bySam = new Dictionary<string, AdAccount>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in SearchRoots())
        {
            if (bySam.Count >= limit) break;
            try
            {
                using (root)
                using (var searcher = new DirectorySearcher(root)
                {
                    // Enabled accounts only (the bit-AND rule excludes ACCOUNTDISABLE) — granting app access
                    // to a disabled account is never intentional.
                    Filter = "(&(objectCategory=person)(objectClass=user)" +
                             "(!(userAccountControl:1.2.840.113556.1.4.803:=2))" +
                             $"(|(sAMAccountName={q}*)(cn={q}*)(givenName={q}*)(sn={q}*)(mail={q}*)))",
                    PageSize = 50,
                    SizeLimit = limit
                })
                {
                    searcher.PropertiesToLoad.AddRange(new[] { "sAMAccountName", "cn", "employeeID", "mail" });
                    searcher.Sort = new SortOption("cn", SortDirection.Ascending);
                    using var found = searcher.FindAll();
                    foreach (SearchResult r in found)
                    {
                        var sam = GetProp(r, "sAMAccountName");
                        if (string.IsNullOrEmpty(sam) || bySam.ContainsKey(sam)) continue;
                        bySam[sam] = new AdAccount(sam, GetProp(r, "cn"), true, GetProp(r, "employeeID"), GetProp(r, "mail"));
                        if (bySam.Count >= limit) break;
                    }
                }
            }
            catch
            {
                // Domain unreachable (off-domain dev box, or trust hiccup) — try the next root.
            }
        }
        return bySam.Values.OrderBy(a => a.Cn, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

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
        // Same two-domain concern as SearchAccounts — vm-trm-live is still joined to iDirectory,
        // so a single default bind misses every account already migrated to ENTERPRISE.AD. Merge
        // by Sam, own-domain result winning on a collision (SearchRoots order), same as SearchAccounts.
        var bySam = new Dictionary<string, AdAccount>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in SearchRoots())
        {
            try
            {
                using (root)
                using (var searcher = new DirectorySearcher(root)
                {
                    // Tremblant users only, enabled and disabled alike; extensionAttribute2 = "T" is
                    // the resort tag confirmed on the live directory.
                    Filter = "(&(objectCategory=person)(objectClass=user)(extensionAttribute2=T))",
                    PageSize = 1000,
                })
                {
                    searcher.PropertiesToLoad.AddRange(new[] { "sAMAccountName", "cn", "userAccountControl", "employeeID", "mail" });

                    using var found = searcher.FindAll();
                    foreach (SearchResult r in found)
                    {
                        var sam = GetProp(r, "sAMAccountName");
                        if (string.IsNullOrEmpty(sam) || bySam.ContainsKey(sam)) continue;

                        var uac = r.Properties["userAccountControl"].Count > 0
                            ? Convert.ToInt32(r.Properties["userAccountControl"][0])
                            : 0;
                        var enabled = (uac & 0x2) == 0; // 0x2 = ACCOUNTDISABLE

                        bySam[sam] = new AdAccount(sam, GetProp(r, "cn"), enabled, GetProp(r, "employeeID"), GetProp(r, "mail"));
                    }
                }
            }
            catch
            {
                // Domain unreachable (off-domain dev box, or trust hiccup) — try the next root.
            }
        }
        return bySam.Values.ToList();
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
