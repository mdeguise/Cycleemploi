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
}

/// <summary>Best-effort e-mail to the fixer through the app's existing SendGrid service — the
/// report is already saved, so a failure here is logged and never reaches the reporter.</summary>
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

    public async Task NotifyAsync(IssueReportDto issue, bool edited, CancellationToken ct)
    {
        var recipients = _options.MailTo
            .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
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
        if (!string.IsNullOrWhiteSpace(_options.AppUrl))
        {
            body.AppendLine();
            body.AppendLine($"Ouvrir : {_options.AppUrl.TrimEnd('/')}/admin/issues?issue={issue.IssueId}");
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
    public static string Build(IssueReportDto i, string? captureBaseUrl)
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
