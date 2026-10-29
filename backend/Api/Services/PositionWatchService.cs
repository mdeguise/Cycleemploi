using Microsoft.EntityFrameworkCore;
using TremblantLifecycle.Api.Data;
using TremblantLifecycle.Api.Models.Entities;

namespace TremblantLifecycle.Api.Services;

public record PositionWatchResult(int Checked, int Changed, int Errors, List<string> Details);

public interface IPositionWatcher
{
    /// <param name="dryRun">true = only report what would be announced: nothing is sent, commented or saved.</param>
    Task<PositionWatchResult> RunOnceAsync(bool dryRun, CancellationToken ct);
}

/// <summary>Réactivation follow-up. When a manager reactivates someone, Workday often still shows the OLD primary
/// position (RH corrects it afterwards), so the TDX ticket IT works from can be stale. This compares each recent
/// reactivation's current Workday primary position with the last one reported; on a change it adds a comment to
/// the employee's TDX ticket (no e-mail: a message to the IT mailbox would open another ticket). Idempotent:
/// PositionLastFollowed records what was already announced.</summary>
public class PositionWatcher(
    AppDbContext db,
    WorkdayContext workday,
    ITdxService tdx,
    ILogger<PositionWatcher> logger) : IPositionWatcher
{
    /// <summary>How long after submission a reactivation is still followed.</summary>
    private static readonly TimeSpan Window = TimeSpan.FromDays(30);

    public async Task<PositionWatchResult> RunOnceAsync(bool dryRun, CancellationToken ct)
    {
        var since = DateTime.UtcNow - Window;
        var candidates = await db.RequestEmployees
            .Include(e => e.Request)
            .Where(e => e.Request.RequestType == RequestType.Reactivation
                        && e.Request.SubmittedAt >= since
                        && e.PositionWorkdayAtSubmit != null)
            .ToListAsync(ct);

        int changed = 0, errors = 0;
        var details = new List<string>();

        foreach (var e in candidates)
        {
            try
            {
                var wd = await workday.WorkdayDemographics.AsNoTracking()
                    .Where(w => w.EmployeeId == e.WorkdayEmployeeId && w.PrimaryJob == true)
                    .Select(w => new { w.PositionTitle, w.JobCode })
                    .FirstOrDefaultAsync(ct);
                if (wd?.PositionTitle is null) continue;
                if (Same(wd.PositionTitle, e.PositionLastFollowed)) continue;

                var ticket = await db.RequestTickets.AsNoTracking().FirstOrDefaultAsync(t =>
                    t.RequestId == e.RequestId && t.Kind == TicketKind.Tdx && t.Outcome == TicketOutcome.Created
                    && (t.RequestEmployeeId == e.RequestEmployeeId || t.RequestEmployeeId == null), ct);
                if (ticket is null || !int.TryParse(ticket.TicketNumber, out var tdxId))
                {
                    // No TDX ticket to update (never created or failed): nothing to announce, just stay in sync.
                    if (!dryRun) { e.PositionLastFollowed = wd.PositionTitle; await db.SaveChangesAsync(ct); }
                    continue;
                }

                var requestNumber = e.Request.RequestNumber;
                var confirmed = Same(wd.PositionTitle, e.PositionSnapshot);
                if (dryRun)
                {
                    changed++;
                    details.Add($"(simulation) {requestNumber}: {e.NameSnapshot} : {e.PositionLastFollowed} → {wd.PositionTitle}{(confirmed ? " [= poste demandé]" : $" [poste demandé : {e.PositionSnapshot}]")} — billet TDX {tdxId}");
                    continue;
                }
                var live = await tdx.GetTicketStatusAsync(tdxId, ct);
                var text =
                    $"Mise à jour du poste dans Workday — réactivation de {e.NameSnapshot} ({e.WorkdayEmployeeId}), demande {requestNumber}.\n" +
                    $"Poste précédent : {e.PositionLastFollowed}\n" +
                    $"Nouveau poste principal dans Workday : {wd.PositionTitle}{(string.IsNullOrWhiteSpace(wd.JobCode) ? "" : $" (code d'emploi {wd.JobCode})")}\n" +
                    (confirmed
                        ? "Il correspond au poste de retour demandé par le gestionnaire.\n"
                        : $"Poste de retour demandé par le gestionnaire : {e.PositionSnapshot}\n") +
                    "Veuillez vérifier les accès et la sécurité en conséquence." +
                    (live.State == LiveTicketState.Closed ? "\n(Ce billet est déjà fermé : le rouvrir au besoin.)" : "");

                // The TDX comment is the only notification: an e-mail to the IT mailbox would open a second TDX ticket.
                // On failure PositionLastFollowed stays as is, so the next hourly run retries.
                var done = false;
                try
                {
                    await tdx.AddFeedEntryAsync(tdxId, text, ct);
                    done = true;
                }
                catch (Exception ex) { logger.LogWarning(ex, "Could not comment TDX ticket {Ticket} for {Request}", tdxId, requestNumber); }

                if (done)
                {
                    e.PositionLastFollowed = wd.PositionTitle;
                    await db.SaveChangesAsync(ct);
                    changed++;
                    details.Add($"{requestNumber}: {e.NameSnapshot} → {wd.PositionTitle} (billet TDX {tdxId})");
                }
                else errors++;
            }
            catch (Exception ex)
            {
                errors++;
                logger.LogError(ex, "Position follow-up failed for request employee {Id}", e.RequestEmployeeId);
            }
        }

        if (changed > 0 || errors > 0)
            logger.LogInformation("Position follow-up: {Checked} checked, {Changed} changed, {Errors} errors", candidates.Count, changed, errors);
        return new PositionWatchResult(candidates.Count, changed, errors, details);
    }

    private static bool Same(string? a, string? b) =>
        string.Equals((a ?? "").Trim(), (b ?? "").Trim(), StringComparison.OrdinalIgnoreCase);
}

/// <summary>Runs the follow-up once an hour (Workday's copy is refreshed overnight; hourly just keeps the delay
/// short and costs one small query). The first run waits two minutes so it never slows application start.</summary>
public class PositionWatchService(IServiceScopeFactory scopes, ILogger<PositionWatchService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken); } catch (OperationCanceledException) { return; }
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IPositionWatcher>().RunOnceAsync(dryRun: false, stoppingToken);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { logger.LogError(ex, "Position follow-up cycle failed"); }

            try { await Task.Delay(TimeSpan.FromHours(1), stoppingToken); } catch (OperationCanceledException) { return; }
        }
    }
}
