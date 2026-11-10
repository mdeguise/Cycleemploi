using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using TremblantLifecycle.Api.Models.Dtos;
using TremblantLifecycle.Api.Models.Entities;

namespace TremblantLifecycle.Api.Services;

/// <summary>"Issues" config section: who is told about a new report, and the SPA's own origin for
/// the deep link in that e-mail (the SPA and API are separate IIS sites).</summary>
public class IssuesOptions
{
    public string MailTo { get; set; } = "";
    public string AppUrl { get; set; } = "";
}

public interface IIssueNotifier
{
    Task NotifyAsync(IssueReportDto issue, bool edited, CancellationToken ct);

    /// <summary>Tells the person who raised a report that it was closed (corrigé / rejeté / doublon),
    /// with the résolution — so they do not have to open "Mes signalements" to find out.</summary>
    Task NotifyReporterAsync(IssueReportDto issue, string? reporterEmail, CancellationToken ct);

    /// <summary>E-mails the other side of the « Échange » thread : the reporter when the team wrote (with the
    /// « ne répondez pas » box and the link to their report), the team (MailTo) otherwise. Best effort.</summary>
    Task NotifyMessageAsync(IssueReportDto issue, IssueMessageDto message, string? reporterEmail, CancellationToken ct);

    /// <summary>E-mails the reporter that the fix is delivered (« À valider ») : check it and click « C'est réglé »
    /// in the app. Best effort.</summary>
    Task NotifyDeliveredAsync(IssueReportDto issue, string? reporterEmail, CancellationToken ct);
}

/// <summary>Best-effort e-mail through the app's existing SendGrid service — the report is already
/// saved, so a failure here is logged and never reaches the reporter. Consigne de Marc (2026-11-10) :
/// every e-mail TO THE REPORTER starts with the « NE RÉPONDEZ PAS À CE COURRIEL » box and links
/// straight to their report ({AppUrl}/?issue=N opens « Mes signalements » on it).</summary>
public class IssueNotifier : IIssueNotifier
{
    private readonly IEmailNotificationService _email;
    private readonly IssuesOptions _options;
    private readonly ILogger<IssueNotifier> _logger;

    public IssueNotifier(IEmailNotificationService email, IOptions<IssuesOptions> options, ILogger<IssueNotifier> logger)
    {
        _email = email;
        _options = options.Value;
        _logger = logger;
    }

    private string? AppBase => string.IsNullOrWhiteSpace(_options.AppUrl) ? null : _options.AppUrl.TrimEnd('/');

    /// <summary>The admin screen on this report (unchanged).</summary>
    private string? AdminLink(int id) => AppBase is { } b ? $"{b}/admin/issues?issue={id}" : null;

    /// <summary>The reporter's « Mes signalements », opened on this report.</summary>
    private string? ReporterLink(int id) => AppBase is { } b ? $"{b}/?issue={id}" : null;

    private List<string> TeamRecipients() => _options.MailTo
        .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToList();

    private static string E(string? v) => WebUtility.HtmlEncode(v ?? string.Empty).Replace("\n", "<br>");

    private static string NoReplyText(string? link) =>
        "============================================================\n"
        + "⚠ NE RÉPONDEZ PAS À CE COURRIEL : votre réponse ne serait pas gardée avec le signalement.\n"
        + (link is null ? "Répondez dans Cycle Emploi : bouton « Signaler un problème », puis « Mes signalements ».\n"
                        : $"Répondez dans l'application, dans le signalement : {link}\n")
        + "============================================================\n\n";

    private static string NoReplyHtml(string? link) =>
        "<div style=\"margin:0 0 1em;padding:.75em 1em;border:2px solid #b45309;background:#fff7ed;color:#7c2d12;font-weight:600\">"
        + "⚠ NE RÉPONDEZ PAS À CE COURRIEL : votre réponse ne serait pas gardée avec le signalement.<br>"
        + (link is null ? "Répondez dans Cycle Emploi : bouton « Signaler un problème », puis « Mes signalements ».</div>"
                        : $"<a href=\"{E(link)}\" style=\"color:#7c2d12\">Répondez dans l'application, dans le signalement</a>.</div>");

    private const string HtmlOpen = "<div style=\"font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#1c2333\">";
    private const string Quote = "<blockquote style=\"margin:0 0 1em;padding:.5em 1em;border-left:3px solid #2f4f8f;background:#f6f8fc\">";

    public async Task NotifyAsync(IssueReportDto issue, bool edited, CancellationToken ct)
    {
        var recipients = TeamRecipients();
        if (recipients.Count == 0) return;

        var name = issue.CreatedByName ?? issue.CreatedBySam;
        var subject = $"Cycle Emploi — signalement #{issue.IssueId} {(edited ? "MODIFIÉ " : "")}({IssueMarkdown.TypeLabel(issue.Type)}) : {issue.Title}";
        var body = new StringBuilder();
        body.AppendLine(edited
            ? $"{name} a modifié son signalement avant qu'il soit traité."
            : $"{name} a signalé un problème dans Cycle Emploi.");
        body.AppendLine();
        body.AppendLine($"Titre : {issue.Title}");
        if (!string.IsNullOrWhiteSpace(issue.Screen)) body.AppendLine($"Écran : {issue.Screen}");
        body.AppendLine();
        body.AppendLine(issue.Description);
        if (!string.IsNullOrWhiteSpace(issue.Expected))
        {
            body.AppendLine();
            body.AppendLine($"Attendu : {issue.Expected}");
        }
        body.AppendLine();
        body.AppendLine($"Captures jointes : {issue.Images.Count}");
        if (issue.Files.Count > 0) body.AppendLine($"Documents joints : {string.Join(", ", issue.Files.Select(f => f.FileName))}");
        if (AdminLink(issue.IssueId) is { } link)
        {
            body.AppendLine();
            body.AppendLine($"Ouvrir : {link}");
        }

        try
        {
            await _email.SendAsync(subject, body.ToString(), recipients, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not e-mail issue report {IssueId}", issue.IssueId);
        }
    }

    public async Task NotifyReporterAsync(IssueReportDto issue, string? reporterEmail, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reporterEmail)) return;

        var outcome = issue.Status switch
        {
            IssueStatus.Fixed => "a été corrigé",
            IssueStatus.Rejected => "n'a pas été retenu",
            IssueStatus.Duplicate => "a déjà été signalé (doublon)",
            _ => "a été traité",
        };
        var link = ReporterLink(issue.IssueId);
        var subject = $"Cycle Emploi — votre signalement #{issue.IssueId} {outcome}";
        var body = new StringBuilder(NoReplyText(link));
        body.AppendLine("Bonjour,");
        body.AppendLine();
        body.AppendLine($"Votre signalement #{issue.IssueId} « {issue.Title} » {outcome}.");
        if (!string.IsNullOrWhiteSpace(issue.Resolution))
        {
            body.AppendLine();
            body.AppendLine("Résolution :");
            body.AppendLine(issue.Resolution);
        }
        body.AppendLine();
        body.AppendLine(link is null
            ? "Vous pouvez aussi le consulter dans Cycle Emploi : bouton « Signaler un problème », puis « Mes signalements »."
            : $"Ouvrir votre signalement : {link}");
        var html = HtmlOpen + NoReplyHtml(link)
            + $"<p>Bonjour,</p><p>Votre signalement #{issue.IssueId} « {E(issue.Title)} » {outcome}.</p>"
            + (string.IsNullOrWhiteSpace(issue.Resolution) ? "" : $"<p><b>Résolution :</b></p>{Quote}{E(issue.Resolution)}</blockquote>")
            + (link is null ? "" : $"<p><a href=\"{E(link)}\">Ouvrir votre signalement #{issue.IssueId}</a></p>")
            + "</div>";

        try
        {
            await _email.SendAsync(subject, body.ToString(), html, [reporterEmail], ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not e-mail the reporter of issue {IssueId}", issue.IssueId);
        }
    }

    public async Task NotifyMessageAsync(IssueReportDto issue, IssueMessageDto m, string? reporterEmail, CancellationToken ct)
    {
        try
        {
            var subject = $"Cycle Emploi — signalement #{issue.IssueId} : {(m.FromTeam ? "réponse de l'équipe" : "réponse de " + m.AuthorName)} — {issue.Title}";
            if (m.FromTeam)
            {
                if (string.IsNullOrWhiteSpace(reporterEmail)) return;
                var link = ReporterLink(issue.IssueId);
                var text = NoReplyText(link)
                    + $"{m.AuthorName} a écrit dans votre signalement #{issue.IssueId} « {issue.Title} » :\n\n{m.Body}\n\n"
                    + (link is null ? "" : $"Pour répondre (la réponse est gardée avec le signalement) : {link}\n");
                var html = HtmlOpen + NoReplyHtml(link)
                    + $"<p><b>{E(m.AuthorName)}</b> a écrit dans votre signalement #{issue.IssueId} « {E(issue.Title)} » :</p>{Quote}{E(m.Body)}</blockquote>"
                    + (link is null ? "" : $"<p><a href=\"{E(link)}\">Répondre dans l'application</a> (la réponse est gardée avec le signalement).</p>")
                    + "</div>";
                await _email.SendAsync(subject, text, html, [reporterEmail], ct);
            }
            else
            {
                var to = TeamRecipients();
                if (to.Count == 0) return;
                var link = AdminLink(issue.IssueId);
                var text = $"{m.AuthorName} a écrit dans le signalement #{issue.IssueId} « {issue.Title} » :\n\n{m.Body}\n\n"
                    + (link is null ? "" : $"Pour répondre (la réponse est gardée avec le signalement) : {link}\n");
                await _email.SendAsync(subject, text, to, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Message added to issue {IssueId}, but the e-mail notice failed", issue.IssueId);
        }
    }

    public async Task NotifyDeliveredAsync(IssueReportDto issue, string? reporterEmail, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reporterEmail)) return;
        try
        {
            var link = ReporterLink(issue.IssueId);
            var subject = $"Cycle Emploi — à vérifier : signalement #{issue.IssueId} « {issue.Title} »";
            var text = NoReplyText(link)
                + $"Le signalement #{issue.IssueId} « {issue.Title} » a été corrigé.\n\n"
                + (string.IsNullOrWhiteSpace(issue.Resolution) ? "" : $"Ce qui a été fait :\n{issue.Resolution}\n\n")
                + "Vérifiez, puis cliquez « C'est réglé » ou « Ce n'est pas réglé » dans le signalement"
                + (link is null ? " (bouton « Signaler un problème », puis « Mes signalements »).\n" : $" :\n{link}\n");
            var html = HtmlOpen + NoReplyHtml(link)
                + $"<p>Le signalement #{issue.IssueId} « {E(issue.Title)} » a été corrigé.</p>"
                + (string.IsNullOrWhiteSpace(issue.Resolution) ? "" : $"<p><b>Ce qui a été fait :</b></p>{Quote}{E(issue.Resolution)}</blockquote>")
                + "<p>Vérifiez, puis cliquez <b>« C'est réglé »</b> ou <b>« Ce n'est pas réglé »</b> dans le signalement"
                + (link is null ? " (bouton « Signaler un problème », puis « Mes signalements »).</p>" : $" : <a href=\"{E(link)}\">ouvrir le signalement #{issue.IssueId}</a>.</p>")
                + "</div>";
            await _email.SendAsync(subject, text, html, [reporterEmail], ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Issue {IssueId} is « À valider », but the reporter could not be e-mailed", issue.IssueId);
        }
    }
}

/// <summary>Labels and the "Copier pour Claude" Markdown — the text an admin pastes into a Claude
/// session (or tools/signalements.ps1 saves to disk) to work a report.</summary>
public static class IssueMarkdown
{
    public static string TypeLabel(string type) => type switch
    {
        IssueType.Bug => "bogue",
        IssueType.Improvement => "amélioration",
        IssueType.Question => "question",
        _ => type,
    };

    public static string StatusLabel(string status) => status switch
    {
        IssueStatus.New => "Nouveau",
        IssueStatus.InProgress => "En cours",
        IssueStatus.Validate => "À valider",
        IssueStatus.Fixed => "Corrigé",
        IssueStatus.Rejected => "Rejeté",
        IssueStatus.Duplicate => "Doublon",
        _ => status,
    };

    public static string Extension(string mime) => mime switch
    {
        "image/jpeg" => "jpg",
        "image/webp" => "webp",
        _ => "png",
    };

    /// <param name="captureBaseUrl">null = refer to files saved next to the .md ("capture-N.ext");
    /// otherwise absolute URLs under this base.</param>
    public static string Build(IssueReportDto i, string? captureBaseUrl, IReadOnlyList<IssueMessageDto>? thread = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Signalement #{i.IssueId} — {i.Title}");
        sb.AppendLine();
        sb.AppendLine("Application : Cycle Emploi (repo mdeguise/Cycleemploi — backend/Api + src)");
        sb.AppendLine($"Type : {TypeLabel(i.Type)} · Statut : {StatusLabel(i.Status)}");
        sb.AppendLine($"Signalé par : {i.CreatedByName ?? i.CreatedBySam} le {i.CreatedUtc:yyyy-MM-dd HH:mm} UTC");
        if (!string.IsNullOrWhiteSpace(i.Screen)) sb.AppendLine($"Écran : {i.Screen}");
        sb.AppendLine();
        sb.AppendLine("## Description");
        sb.AppendLine(i.Description);
        if (!string.IsNullOrWhiteSpace(i.Expected))
        {
            sb.AppendLine();
            sb.AppendLine("## Comportement attendu");
            sb.AppendLine(i.Expected);
        }
        if (thread is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("## Échange (ce qui s'est dit et décidé)");
            foreach (var m in thread)
            {
                sb.AppendLine();
                sb.AppendLine($"**{m.CreatedUtc:yyyy-MM-dd HH:mm} UTC — {m.AuthorName ?? m.AuthorSam ?? (m.FromTeam ? "Équipe" : "Demandeur")} ({(m.FromTeam ? "équipe" : "demandeur")})**");
                sb.AppendLine(m.Body);
            }
        }

        if (i.Images.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Captures");
            for (var n = 0; n < i.Images.Count; n++)
            {
                var img = i.Images[n];
                var href = captureBaseUrl is null
                    ? $"capture-{n + 1}.{Extension(img.MimeType)}"
                    : $"{captureBaseUrl}/{i.IssueId}/images/{img.ImageId}";
                sb.AppendLine($"- {href}");
            }
        }

        if (i.Files.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Documents joints");
            for (var n = 0; n < i.Files.Count; n++)
            {
                var f = i.Files[n];
                var href = captureBaseUrl is null
                    ? $"document-{n + 1}-{f.FileName}"
                    : $"{captureBaseUrl}/{i.IssueId}/files/{f.FileId}";
                sb.AppendLine($"- {href}");
            }
        }

        if (!string.IsNullOrWhiteSpace(i.Context))
        {
            sb.AppendLine();
            sb.AppendLine("## Contexte technique (capturé automatiquement)");
            sb.AppendLine("```json");
            sb.AppendLine(i.Context);
            sb.AppendLine("```");
        }

        if (!string.IsNullOrWhiteSpace(i.Resolution))
        {
            sb.AppendLine();
            sb.AppendLine("## Résolution");
            sb.AppendLine(i.Resolution);
        }
        if (!string.IsNullOrWhiteSpace(i.WorkLog))
        {
            sb.AppendLine();
            sb.AppendLine("## Travaux déjà consignés");
            sb.AppendLine(i.WorkLog);
        }

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine("Pour clore : `.\\tools\\signalements.ps1 -Id " + i.IssueId + " -Statut FIXED -Commit <sha> -Resolution \"…\" -Travaux \"<fichiers + logique + raison>\"`");
        return sb.ToString();
    }
}
