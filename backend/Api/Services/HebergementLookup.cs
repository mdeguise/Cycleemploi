using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace TremblantLifecycle.Api.Services;

/// <summary>"Hebergement" config section. Enabled = false until the HebergementEmployes database exists
/// (D:\Github\HebergementEmployes) — then the offboarding "RH - Hébergement" ticket is switched on.</summary>
public class HebergementOptions
{
    public bool Enabled { get; set; }
}

/// <summary>One lodging of an employee, from the weekly Excel list (HebergementEmployes.dbo.HebergementEmploye).</summary>
public record HebergementInfo(string WorkdayEmployeeId, string CusId, string Logement);

public interface IHebergementLookup
{
    bool Enabled { get; }

    /// <summary>The lodgings of these Workday employees (none for an employee not on the list). Throws if the
    /// database is unreachable — the caller records that as a failed ticket rather than silently skipping.</summary>
    Task<IReadOnlyList<HebergementInfo>> ForEmployeesAsync(IEnumerable<string> workdayEmployeeIds, CancellationToken ct);
}

public class HebergementLookup(IConfiguration config, IOptions<HebergementOptions> options) : IHebergementLookup
{
    public bool Enabled => options.Value.Enabled;

    public async Task<IReadOnlyList<HebergementInfo>> ForEmployeesAsync(IEnumerable<string> workdayEmployeeIds, CancellationToken ct)
    {
        var ids = workdayEmployeeIds.Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => i.Trim()).Distinct().ToList();
        if (!Enabled || ids.Count == 0) return [];

        await using var cn = new SqlConnection(config.GetConnectionString("HebergementDb"));
        await cn.OpenAsync(ct);
        await using var cmd = cn.CreateCommand();
        var names = ids.Select((_, i) => $"@p{i}").ToList();
        cmd.CommandText = $"SELECT WorkdayEmployeeId, CusId, Logement FROM dbo.HebergementEmploye WHERE WorkdayEmployeeId IN ({string.Join(",", names)}) ORDER BY WorkdayEmployeeId, CusId, Logement";
        for (var i = 0; i < ids.Count; i++) cmd.Parameters.AddWithValue(names[i], ids[i]);

        var rows = new List<HebergementInfo>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            rows.Add(new HebergementInfo(r.GetString(0), r.GetString(1), r.GetString(2)));
        return rows;
    }
}
