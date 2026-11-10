using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TremblantLifecycle.Api.Data;
using TremblantLifecycle.Api.Models.Dtos;
using TremblantLifecycle.Api.Models.Entities;
using TremblantLifecycle.Api.Services;

namespace TremblantLifecycle.Api.Controllers;

/// <summary>"Signaler un problème" — any signed-in user creates reports and sees/edits their own
/// (while still NEW); an AppUsers Admin sees every report, follows them up and exports. Same
/// design as PO Tool's IssueReportsController, adapted to this app's identity/role model.</summary>
[ApiController]
[Route("api/issues")]
[Authorize]
public class IssueReportsController : ControllerBase
{
    private const int MaxCaptures = 4;
    private const int MaxCaptureBytes = 4 * 1024 * 1024;
    private const int MaxDocuments = 3;
    private const int MaxDocumentBytes = 5 * 1024 * 1024;
    // Base64 inflates ~33%, and IIS refuses a request body over 30 MB by default — stay well under.
    private const int MaxTotalBytes = 15 * 1024 * 1024;

    private static readonly string[] ImageMimes = ["image/png", "image/jpeg", "image/webp"];
    private static readonly Dictionary<string, string> DocumentMimes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    };

    private readonly AppDbContext _db;
    private readonly IAppUserService _appUsers;
    private readonly IAdDirectoryService _ad;
    private readonly IIssueNotifier _notifier;

    public IssueReportsController(AppDbContext db, IAppUserService appUsers, IAdDirectoryService ad, IIssueNotifier notifier)
    {
        _db = db;
        _appUsers = appUsers;
        _ad = ad;
        _notifier = notifier;
    }

    private string Sam => AppUserService.Normalize(User.GetObjectId());
    private Task<bool> IsAdminAsync(CancellationToken ct) => _appUsers.IsAdminAsync(User.GetObjectId(), ct);

    private async Task<bool> CanSeeAsync(IssueReport i, CancellationToken ct) =>
        string.Equals(i.CreatedBySam, Sam, StringComparison.OrdinalIgnoreCase) || await IsAdminAsync(ct);

    private static IssueReportDto ToDto(IssueReport i) => new()
    {
        IssueId = i.IssueId,
        CreatedUtc = i.CreatedUtc,
        CreatedBySam = i.CreatedBySam,
        CreatedByName = i.CreatedByName,
        Type = i.Type,
        Title = i.Title,
        Description = i.Description,
        Expected = i.Expected,
        Screen = i.Screen,
        Context = i.Context,
        Status = i.Status,
        Resolution = i.Resolution,
        CommitRef = i.CommitRef,
        WorkLog = i.WorkLog,
        HandledUtc = i.HandledUtc,
        HandledBySam = i.HandledBySam,
        Images = i.Images.Where(x => IsImage(x.MimeType)).OrderBy(x => x.Ordinal)
            .Select(x => new IssueImageDto(x.ImageId, x.MimeType)).ToList(),
        Files = i.Images.Where(x => !IsImage(x.MimeType)).OrderBy(x => x.Ordinal)
            .Select(x => new IssueFileDto(x.ImageId, x.FileName ?? "document", x.MimeType, x.Size)).ToList(),
    };

    private static bool IsImage(string mime) => mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    // The Images collection holds BYTES — never load them for list/detail DTOs.
    private IQueryable<IssueReport> WithAttachmentMeta() =>
        _db.IssueReports.AsNoTracking().Select(i => new IssueReport
        {
            IssueId = i.IssueId, CreatedUtc = i.CreatedUtc, CreatedBySam = i.CreatedBySam, CreatedByName = i.CreatedByName,
            Type = i.Type, Title = i.Title, Description = i.Description, Expected = i.Expected, Screen = i.Screen,
            Context = i.Context, Status = i.Status, Resolution = i.Resolution, CommitRef = i.CommitRef, WorkLog = i.WorkLog,
            HandledUtc = i.HandledUtc, HandledBySam = i.HandledBySam,
            Images = i.Images.Select(x => new IssueReportImage
            {
                ImageId = x.ImageId, IssueId = x.IssueId, Ordinal = x.Ordinal, MimeType = x.MimeType, FileName = x.FileName, Size = x.Size,
            }).ToList(),
        });

    private async Task<IssueReportDto?> LoadDtoAsync(int id, CancellationToken ct)
    {
        var i = await WithAttachmentMeta().FirstOrDefaultAsync(x => x.IssueId == id, ct);
        if (i is null) return null;
        var dto = ToDto(i);
        await AttachThreadAsync([dto], ct);
        return dto;
    }

    /// <summary>Fil « Échange » : nombre de messages, date du dernier, et s'il vient de l'équipe — c'est ce qui fait
    /// passer le bouton du haut à l'orange (« un signalement attend votre réponse »).</summary>
    private async Task AttachThreadAsync(IReadOnlyCollection<IssueReportDto> dtos, CancellationToken ct)
    {
        if (dtos.Count == 0) return;
        var ids = dtos.Select(d => d.IssueId).ToList();
        var rows = await _db.IssueReportMessages.AsNoTracking().Where(m => ids.Contains(m.IssueId))
            .Select(m => new { m.IssueId, m.MessageId, m.CreatedUtc, m.FromTeam }).ToListAsync(ct);
        foreach (var g in rows.GroupBy(r => r.IssueId))
        {
            var dto = dtos.First(d => d.IssueId == g.Key);
            var last = g.OrderByDescending(r => r.CreatedUtc).ThenByDescending(r => r.MessageId).First();
            dto.MessageCount = g.Count();
            dto.LastMessageUtc = last.CreatedUtc;
            dto.LastMessageFromTeam = last.FromTeam;
        }
    }

    private static IssueMessageDto MessageDto(IssueReportMessage m) => new(m.MessageId, m.CreatedUtc, m.FromTeam, m.AuthorSam, m.AuthorName, m.Body);

    private string DisplayName => _ad.GetUserInfo(User.GetSamAccountName()).DisplayName ?? Sam;

    // ---- attachment decoding -------------------------------------------------------------

    private static bool TryDecodeCapture(string dataUrl, out string mime, out byte[] bytes)
    {
        mime = ""; bytes = [];
        var m = Regex.Match(dataUrl ?? "", @"^data:(?<mime>[\w/+.-]+);base64,(?<b64>.+)$", RegexOptions.Singleline);
        if (!m.Success) return false;
        mime = m.Groups["mime"].Value.ToLowerInvariant();
        if (!ImageMimes.Contains(mime)) return false;
        try { bytes = Convert.FromBase64String(m.Groups["b64"].Value); }
        catch (FormatException) { return false; }
        return bytes.Length > 0 && bytes.Length <= MaxCaptureBytes;
    }

    private static string SanitizeFileName(string name)
    {
        var n = Path.GetFileName((name ?? "").Replace('\\', '/'));
        n = Regex.Replace(n, @"[\x00-\x1f""<>|:*?/\\]", "");
        return n.Length > 150 ? n[^150..] : n;
    }

    private static bool TryDecodeFile(IssueFileInput f, out string fileName, out string mime, out byte[] bytes)
    {
        fileName = ""; mime = ""; bytes = [];
        fileName = SanitizeFileName(f.FileName);
        var ext = Path.GetExtension(fileName);
        if (fileName.Length == 0 || !DocumentMimes.TryGetValue(ext, out var m)) return false;
        mime = m;
        var dm = Regex.Match(f.DataUrl ?? "", @"^data:[^;,]*(;[^;,]*)*;base64,(?<b64>.+)$", RegexOptions.Singleline);
        if (!dm.Success) return false;
        try { bytes = Convert.FromBase64String(dm.Groups["b64"].Value); }
        catch (FormatException) { return false; }
        return bytes.Length is > 0 and <= MaxDocumentBytes && MatchesFormat(ext.ToLowerInvariant(), bytes);
    }

    /// <summary>Content check — the extension decides the stored MIME type, so make sure the bytes
    /// really are that kind of document (never trust a client-supplied type).</summary>
    private static bool MatchesFormat(string ext, byte[] b)
    {
        switch (ext)
        {
            case ".pdf":
                return Encoding.ASCII.GetString(b, 0, Math.Min(b.Length, 1024)).Contains("%PDF-", StringComparison.Ordinal);
            case ".doc":
            case ".xls":
                return b.Length > 8 && b[0] == 0xD0 && b[1] == 0xCF && b[2] == 0x11 && b[3] == 0xE0
                    && b[4] == 0xA1 && b[5] == 0xB1 && b[6] == 0x1A && b[7] == 0xE1;
            case ".docx":
            case ".xlsx":
                try
                {
                    using var zip = new ZipArchive(new MemoryStream(b), ZipArchiveMode.Read);
                    var prefix = ext == ".docx" ? "word/" : "xl/";
                    return zip.GetEntry("[Content_Types].xml") is not null
                        && zip.Entries.Any(e => e.FullName.StartsWith(prefix, StringComparison.Ordinal));
                }
                catch (InvalidDataException) { return false; }
            default:
                return false;
        }
    }

    private static string? ValidateCommon(string type, string title, string description) =>
        !IssueType.All.Contains(type) ? "Type de signalement invalide."
        : string.IsNullOrWhiteSpace(title) ? "Le titre est requis."
        : string.IsNullOrWhiteSpace(description) ? "La description est requise."
        : null;

    /// <summary>Decodes new attachments into entities; returns an error message, or null.</summary>
    private static string? BuildAttachments(IEnumerable<string> captures, IEnumerable<IssueFileInput> files, int keptCaptures, int keptDocs, long keptBytes, byte startOrdinal, out List<IssueReportImage> result)
    {
        result = [];
        var caps = captures.ToList();
        var docs = files.ToList();
        if (keptCaptures + caps.Count > MaxCaptures) return $"Maximum {MaxCaptures} captures.";
        if (keptDocs + docs.Count > MaxDocuments) return $"Maximum {MaxDocuments} documents.";

        long total = keptBytes;
        var ordinal = startOrdinal;
        foreach (var c in caps)
        {
            if (!TryDecodeCapture(c, out var mime, out var bytes)) return "Capture invalide (PNG, JPEG ou WebP, 4 Mo maximum).";
            total += bytes.Length;
            result.Add(new IssueReportImage { Ordinal = ordinal++, MimeType = mime, Size = bytes.Length, Content = bytes });
        }
        foreach (var f in docs)
        {
            if (!TryDecodeFile(f, out var name, out var mime, out var bytes)) return $"Document invalide : « {SanitizeFileName(f.FileName)} » (PDF, Word ou Excel, 5 Mo maximum).";
            total += bytes.Length;
            result.Add(new IssueReportImage { Ordinal = ordinal++, MimeType = mime, FileName = name, Size = bytes.Length, Content = bytes });
        }
        return total > MaxTotalBytes ? "Les pièces jointes dépassent 15 Mo au total." : null;
    }

    // ---- endpoints -----------------------------------------------------------------------

    [HttpPost]
    public async Task<ActionResult<IssueReportDto>> Create(IssueReportInput input, CancellationToken ct)
    {
        var invalid = ValidateCommon(input.Type, input.Title, input.Description);
        if (invalid is not null) return BadRequest(new { error = invalid });

        var attachError = BuildAttachments(input.Captures, input.Files, 0, 0, 0, 0, out var attachments);
        if (attachError is not null) return BadRequest(new { error = attachError });

        var title = input.Title.Trim();
        var issue = new IssueReport
        {
            CreatedUtc = DateTime.UtcNow,
            CreatedBySam = Sam,
            CreatedByName = _ad.GetUserInfo(User.GetSamAccountName()).DisplayName ?? Sam,
            Type = input.Type,
            Title = title.Length > 200 ? title[..200] : title,
            Description = input.Description.Trim(),
            Expected = string.IsNullOrWhiteSpace(input.Expected) ? null : input.Expected.Trim(),
            Screen = input.Screen is { Length: > 400 } ? input.Screen[..400] : input.Screen,
            Context = input.Context,
            Status = IssueStatus.New,
            Images = attachments,
        };
        _db.IssueReports.Add(issue);
        await _db.SaveChangesAsync(ct);

        var dto = await LoadDtoAsync(issue.IssueId, ct);
        await _notifier.NotifyAsync(dto!, edited: false, ct);
        return Ok(dto);
    }

    [HttpPut("{id:int}/edit")]
    public async Task<ActionResult<IssueReportDto>> Edit(int id, IssueEditInput input, CancellationToken ct)
    {
        var issue = await _db.IssueReports.Include(i => i.Images).FirstOrDefaultAsync(i => i.IssueId == id, ct);
        if (issue is null) return NotFound();
        if (!await CanSeeAsync(issue, ct)) return Forbid();
        if (issue.Status != IssueStatus.New)
            return Conflict(new { error = "Ce signalement est déjà pris en charge — il ne peut plus être modifié." });

        var invalid = ValidateCommon(input.Type, input.Title, input.Description);
        if (invalid is not null) return BadRequest(new { error = invalid });

        var remove = issue.Images.Where(x => input.RemoveAttachmentIds.Contains(x.ImageId)).ToList();
        var kept = issue.Images.Except(remove).ToList();
        var keptCaptures = kept.Count(x => IsImage(x.MimeType));
        var keptDocs = kept.Count - keptCaptures;
        var nextOrdinal = (byte)(kept.Count == 0 ? 0 : kept.Max(x => x.Ordinal) + 1);

        var attachError = BuildAttachments(input.Captures, input.Files, keptCaptures, keptDocs, kept.Sum(x => (long)x.Size), nextOrdinal, out var added);
        if (attachError is not null) return BadRequest(new { error = attachError });

        _db.IssueReportImages.RemoveRange(remove);
        foreach (var a in added) issue.Images.Add(a);

        var title = input.Title.Trim();
        issue.Type = input.Type;
        issue.Title = title.Length > 200 ? title[..200] : title;
        issue.Description = input.Description.Trim();
        issue.Expected = string.IsNullOrWhiteSpace(input.Expected) ? null : input.Expected.Trim();
        await _db.SaveChangesAsync(ct);

        var dto = await LoadDtoAsync(id, ct);
        await _notifier.NotifyAsync(dto!, edited: true, ct);
        return Ok(dto);
    }

    [HttpGet("mine")]
    public async Task<ActionResult<List<IssueReportDto>>> Mine(CancellationToken ct)
    {
        var sam = Sam;
        var rows = await WithAttachmentMeta().Where(i => i.CreatedBySam == sam).OrderByDescending(i => i.CreatedUtc).ToListAsync(ct);
        var dtos = rows.Select(ToDto).ToList();
        await AttachThreadAsync(dtos, ct);
        return Ok(dtos);
    }

    /// <summary>status = "open" (NEW + IN_PROGRESS), a status code, or omitted for everything.</summary>
    [HttpGet]
    public async Task<ActionResult<List<IssueReportDto>>> List([FromQuery] string? status, CancellationToken ct)
    {
        if (!await IsAdminAsync(ct)) return Forbid();
        var q = WithAttachmentMeta();
        if (string.Equals(status, "open", StringComparison.OrdinalIgnoreCase))
            q = q.Where(i => i.Status == IssueStatus.New || i.Status == IssueStatus.InProgress);
        else if (!string.IsNullOrWhiteSpace(status) && IssueStatus.All.Contains(status.ToUpperInvariant()))
        {
            var s = status.ToUpperInvariant();
            q = q.Where(i => i.Status == s);
        }
        var rows = await q.ToListAsync(ct);
        int Rank(string s) => s == IssueStatus.New ? 0 : s == IssueStatus.InProgress ? 1 : 2;
        var dtos = rows.OrderBy(i => Rank(i.Status)).ThenByDescending(i => i.CreatedUtc).Select(ToDto).ToList();
        await AttachThreadAsync(dtos, ct);
        return Ok(dtos);
    }

    [HttpGet("open-count")]
    public async Task<ActionResult<IssueCountDto>> OpenCount(CancellationToken ct)
    {
        if (!await IsAdminAsync(ct)) return Forbid();
        var n = await _db.IssueReports.AsNoTracking().CountAsync(i => i.Status == IssueStatus.New || i.Status == IssueStatus.InProgress, ct);
        return Ok(new IssueCountDto { Count = n });
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<IssueReportDto>> Get(int id, CancellationToken ct)
    {
        var dto = await LoadDtoAsync(id, ct);
        if (dto is null) return NotFound();
        if (!string.Equals(dto.CreatedBySam, Sam, StringComparison.OrdinalIgnoreCase) && !await IsAdminAsync(ct)) return Forbid();
        return Ok(dto);
    }

    [HttpGet("{id:int}/images/{imageId:int}")]
    public async Task<IActionResult> Image(int id, int imageId, CancellationToken ct)
    {
        var issue = await _db.IssueReports.AsNoTracking().FirstOrDefaultAsync(i => i.IssueId == id, ct);
        if (issue is null) return NotFound();
        if (!await CanSeeAsync(issue, ct)) return Forbid();
        var img = await _db.IssueReportImages.AsNoTracking().FirstOrDefaultAsync(x => x.IssueId == id && x.ImageId == imageId, ct);
        if (img is null || !IsImage(img.MimeType)) return NotFound();
        return File(img.Content, img.MimeType);
    }

    [HttpGet("{id:int}/files/{fileId:int}")]
    public async Task<IActionResult> FileDownload(int id, int fileId, CancellationToken ct)
    {
        var issue = await _db.IssueReports.AsNoTracking().FirstOrDefaultAsync(i => i.IssueId == id, ct);
        if (issue is null) return NotFound();
        if (!await CanSeeAsync(issue, ct)) return Forbid();
        var f = await _db.IssueReportImages.AsNoTracking().FirstOrDefaultAsync(x => x.IssueId == id && x.ImageId == fileId, ct);
        if (f is null || IsImage(f.MimeType)) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(f.Content, f.MimeType, f.FileName ?? "document");
    }

    [HttpGet("{id:int}/markdown")]
    public async Task<IActionResult> Markdown(int id, [FromQuery] string? captures, CancellationToken ct)
    {
        if (!await IsAdminAsync(ct)) return Forbid();
        var dto = await LoadDtoAsync(id, ct);
        if (dto is null) return NotFound();
        var baseUrl = string.Equals(captures, "files", StringComparison.OrdinalIgnoreCase)
            ? null
            : $"{Request.Scheme}://{Request.Host}/api/issues";
        var thread = (await _db.IssueReportMessages.AsNoTracking().Where(m => m.IssueId == id)
            .OrderBy(m => m.CreatedUtc).ThenBy(m => m.MessageId).ToListAsync(ct)).Select(MessageDto).ToList();
        return Content(IssueMarkdown.Build(dto, baseUrl, thread), "text/markdown", Encoding.UTF8);
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] string? status, CancellationToken ct)
    {
        if (!await IsAdminAsync(ct)) return Forbid();
        var q = _db.IssueReports.AsNoTracking().AsQueryable();
        var label = "tous";
        if (string.Equals(status, "open", StringComparison.OrdinalIgnoreCase))
        {
            q = q.Where(i => i.Status == IssueStatus.New || i.Status == IssueStatus.InProgress);
            label = "ouverts";
        }
        else if (!string.IsNullOrWhiteSpace(status) && IssueStatus.All.Contains(status.ToUpperInvariant()))
        {
            var s = status.ToUpperInvariant();
            q = q.Where(i => i.Status == s);
            label = IssueMarkdown.StatusLabel(s);
        }
        var rows = await q.OrderBy(i => i.IssueId).Select(i => new
        {
            i.IssueId, i.CreatedUtc, i.Type, i.Status, i.Title, i.CreatedByName, i.CreatedBySam, i.Screen, i.Description,
            i.Expected, i.Resolution, i.CommitRef, i.HandledUtc, i.HandledBySam, i.WorkLog,
            Captures = i.Images.Count(x => x.MimeType.StartsWith("image/")),
            Docs = i.Images.Where(x => !x.MimeType.StartsWith("image/")).Select(x => x.FileName).ToList(),
        }).ToListAsync(ct);

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Signalements");
        ws.Cell(1, 1).Value = $"{rows.Count} signalement(s) — {label} — exporté le {DateTime.Now:yyyy-MM-dd HH:mm}";
        ws.Cell(1, 1).Style.Font.Italic = true;
        string[] headers = ["No", "Date", "Type", "Statut", "Titre", "Signalé par", "Écran", "Description", "Attendu", "Résolution", "Commit", "Traité le", "Traité par", "Travaux", "Captures", "Documents joints"];
        for (var c = 0; c < headers.Length; c++) ws.Cell(2, c + 1).Value = headers[c];
        ws.Row(2).Style.Font.Bold = true;
        var r = 3;
        foreach (var i in rows)
        {
            ws.Cell(r, 1).Value = i.IssueId;
            ws.Cell(r, 2).Value = i.CreatedUtc.ToLocalTime();
            ws.Cell(r, 3).Value = IssueMarkdown.TypeLabel(i.Type);
            ws.Cell(r, 4).Value = IssueMarkdown.StatusLabel(i.Status);
            ws.Cell(r, 5).Value = i.Title;
            ws.Cell(r, 6).Value = i.CreatedByName ?? i.CreatedBySam;
            ws.Cell(r, 7).Value = i.Screen ?? "";
            ws.Cell(r, 8).Value = i.Description;
            ws.Cell(r, 9).Value = i.Expected ?? "";
            ws.Cell(r, 10).Value = i.Resolution ?? "";
            ws.Cell(r, 11).Value = i.CommitRef ?? "";
            if (i.HandledUtc is { } h) ws.Cell(r, 12).Value = h.ToLocalTime();
            ws.Cell(r, 13).Value = i.HandledBySam ?? "";
            ws.Cell(r, 14).Value = i.WorkLog ?? "";
            ws.Cell(r, 15).Value = i.Captures;
            ws.Cell(r, 16).Value = string.Join(", ", i.Docs);
            r++;
        }
        ws.Range(2, 1, Math.Max(r - 1, 2), headers.Length).SetAutoFilter();
        ws.SheetView.FreezeRows(2);
        foreach (var col in new[] { 5, 8, 9, 10, 14 })
        {
            ws.Column(col).Width = 45;
            ws.Column(col).Style.Alignment.WrapText = true;
        }
        ws.Column(1).Width = 6;
        ws.Column(2).Width = 17;
        ws.Column(12).Width = 17;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Signalements_{DateTime.Now:yyyy-MM-dd}.xlsx");
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<IssueReportDto>> Update(int id, IssueUpdateInput input, CancellationToken ct)
    {
        if (!await IsAdminAsync(ct)) return Forbid();
        var issue = await _db.IssueReports.FirstOrDefaultAsync(i => i.IssueId == id, ct);
        if (issue is null) return NotFound();

        var status = (input.Status ?? "").ToUpperInvariant();
        if (!IssueStatus.All.Contains(status)) return BadRequest(new { error = "Statut invalide." });

        var newWork = input.WorkLog?.Trim();
        // « À valider » (correctif livré) comme « Corrigé » : la résolution est ce que la personne lira, les travaux sont
        // le journal technique de la correction.
        if (status is IssueStatus.Fixed or IssueStatus.Validate)
        {
            if (string.IsNullOrWhiteSpace(input.Resolution))
                return BadRequest(new { error = "Une résolution est requise pour livrer ou fermer un signalement." });
            if (string.IsNullOrWhiteSpace(newWork) && string.IsNullOrWhiteSpace(issue.WorkLog))
                return BadRequest(new { error = "Les travaux effectués sont requis pour livrer ou fermer un signalement." });
        }

        var previousStatus = issue.Status;
        issue.Status = status;
        issue.Resolution = string.IsNullOrWhiteSpace(input.Resolution) ? null : input.Resolution.Trim();
        var commit = input.CommitRef?.Trim();
        issue.CommitRef = string.IsNullOrEmpty(commit) ? null : (commit.Length > 40 ? commit[..40] : commit);
        if (!string.IsNullOrWhiteSpace(newWork))
        {
            var stamp = $"[{DateTime.Now:yyyy-MM-dd HH:mm}{(issue.CommitRef is null ? "" : " · commit " + issue.CommitRef)}]\n{newWork}";
            issue.WorkLog = string.IsNullOrWhiteSpace(issue.WorkLog) ? stamp : issue.WorkLog + "\n\n" + stamp;
        }
        if (status == IssueStatus.New)
        {
            issue.HandledUtc = null;
            issue.HandledBySam = null;
        }
        else
        {
            issue.HandledUtc = DateTime.UtcNow;
            issue.HandledBySam = Sam;
        }
        await _db.SaveChangesAsync(ct);
        var dto = await LoadDtoAsync(id, ct);

        // Tell the reporter once, when the report becomes closed — not on every later re-save.
        var closing = status is IssueStatus.Fixed or IssueStatus.Rejected or IssueStatus.Duplicate;
        if (closing && previousStatus != status)
        {
            var reporterEmail = _ad.GetUserInfo(issue.CreatedBySam).Email;
            await _notifier.NotifyReporterAsync(dto!, reporterEmail, ct);
        }
        // Livré (« À valider ») : la personne qui a signalé vérifie et clique « C'est réglé » dans l'application.
        if (status == IssueStatus.Validate && previousStatus != status)
            await _notifier.NotifyDeliveredAsync(dto!, _ad.GetUserInfo(issue.CreatedBySam).Email, ct);
        return Ok(dto);
    }

    /// <summary>La réponse de la personne qui a signalé à un correctif livré (« À valider ») : « C'est réglé » → FIXED
    /// (Corrigé), le signalement est fermé ; « Ce n'est pas réglé » → IN_PROGRESS, il retourne à l'équipe. Inscrit dans
    /// l'échange, l'équipe est avisée. Seule la personne qui a signalé peut répondre (consigne de Marc, 2026-11-10).</summary>
    [HttpPost("{id:int}/confirmation")]
    public async Task<ActionResult<IssueReportDto>> Confirm(int id, IssueConfirmInput input, CancellationToken ct)
    {
        var issue = await _db.IssueReports.FirstOrDefaultAsync(i => i.IssueId == id, ct);
        if (issue is null) return NotFound();
        if (!string.Equals(issue.CreatedBySam, Sam, StringComparison.OrdinalIgnoreCase)) return Forbid();
        if (issue.Status != IssueStatus.Validate)
            return BadRequest(new { error = "Ce signalement n'attend pas votre confirmation (il n'est pas « À valider »)." });
        var comment = (input.Commentaire ?? "").Trim();
        if (comment.Length > 8000) return BadRequest(new { error = "Commentaire trop long (8 000 caractères au plus)." });

        issue.Status = input.Regle ? IssueStatus.Fixed : IssueStatus.InProgress;
        issue.HandledUtc = DateTime.UtcNow;
        var name = DisplayName;
        var msg = new IssueReportMessage
        {
            IssueId = id, CreatedUtc = DateTime.UtcNow, FromTeam = false, AuthorSam = Sam,
            AuthorName = name.Length > 200 ? name[..200] : name,
            Body = (input.Regle ? "C'est réglé." : "Ce n'est pas réglé.") + (comment.Length > 0 ? $"\n{comment}" : ""),
        };
        _db.IssueReportMessages.Add(msg);
        await _db.SaveChangesAsync(ct);

        var dto = (await LoadDtoAsync(id, ct))!;
        await _notifier.NotifyMessageAsync(dto, MessageDto(msg), null, ct);
        return Ok(dto);
    }

    // ---- Fil « Échange » : ce qui s'est dit et décidé, gardé avec le signalement ----------

    /// <summary>Le fil d'un signalement : visible par la personne qui l'a signalé et par les administrateurs.</summary>
    [HttpGet("{id:int}/messages")]
    public async Task<ActionResult<List<IssueMessageDto>>> Messages(int id, CancellationToken ct)
    {
        var issue = await _db.IssueReports.AsNoTracking().FirstOrDefaultAsync(i => i.IssueId == id, ct);
        if (issue is null) return NotFound();
        if (!await CanSeeAsync(issue, ct)) return Forbid();
        var rows = await _db.IssueReportMessages.AsNoTracking().Where(m => m.IssueId == id)
            .OrderBy(m => m.CreatedUtc).ThenBy(m => m.MessageId).ToListAsync(ct);
        return Ok(rows.Select(MessageDto).ToList());
    }

    /// <summary>Ajoute un message au fil. La personne qui a signalé écrit « du côté du demandeur » ; un administrateur
    /// qui n'en est pas l'auteur écrit « du côté de l'équipe ». Un courriel d'avis part à l'autre côté (au mieux).</summary>
    [HttpPost("{id:int}/messages")]
    public async Task<ActionResult<IssueMessageDto>> PostMessage(int id, IssueMessageInput input, CancellationToken ct)
    {
        var issue = await _db.IssueReports.AsNoTracking().FirstOrDefaultAsync(i => i.IssueId == id, ct);
        if (issue is null) return NotFound();
        if (!await CanSeeAsync(issue, ct)) return Forbid();
        var text = (input.Body ?? "").Trim();
        if (text.Length == 0) return BadRequest(new { error = "Écrivez un message." });
        if (text.Length > 8000) return BadRequest(new { error = "Message trop long (8 000 caractères au plus)." });

        var fromTeam = !string.Equals(issue.CreatedBySam, Sam, StringComparison.OrdinalIgnoreCase);
        var name = DisplayName;
        var msg = new IssueReportMessage
        {
            IssueId = id, CreatedUtc = DateTime.UtcNow, FromTeam = fromTeam, AuthorSam = Sam,
            AuthorName = name.Length > 200 ? name[..200] : name, Body = text,
        };
        _db.IssueReportMessages.Add(msg);
        await _db.SaveChangesAsync(ct);

        var mdto = MessageDto(msg);
        if (input.Notify != false)
        {
            var dto = (await LoadDtoAsync(id, ct))!;
            var reporterEmail = fromTeam ? _ad.GetUserInfo(issue.CreatedBySam).Email : null;
            await _notifier.NotifyMessageAsync(dto, mdto, reporterEmail, ct);
        }
        return Ok(mdto);
    }
}
