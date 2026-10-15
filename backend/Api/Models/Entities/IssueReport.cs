namespace TremblantLifecycle.Api.Models.Entities;

/// <summary>A "Signaler un problème" report raised from inside the app (bug / amélioration /
/// question), with auto-captured context. Same design as PO Tool/Redingote. Handled by an Admin
/// (AppUsers) through Administration > Signalements or tools/signalements.ps1.</summary>
public class IssueReport
{
    public int IssueId { get; set; }
    public DateTime CreatedUtc { get; set; }
    public string CreatedBySam { get; set; } = null!;
    public string? CreatedByName { get; set; }
    public string Type { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string? Expected { get; set; }
    public string? Screen { get; set; }
    /// <summary>JSON snapshot taken when the modal opened (version, url, screen, browser, last errors).</summary>
    public string? Context { get; set; }
    public string Status { get; set; } = IssueStatus.New;
    public string? Resolution { get; set; }
    public string? CommitRef { get; set; }
    /// <summary>Append-only, each pass stamped "[date · commit sha]" — what was changed and why.</summary>
    public string? WorkLog { get; set; }
    public DateTime? HandledUtc { get; set; }
    public string? HandledBySam { get; set; }

    public List<IssueReportImage> Images { get; set; } = [];
}

/// <summary>Screenshots AND documents share this table — an image when MimeType starts with
/// "image/", a document otherwise (FileName is set for documents).</summary>
public class IssueReportImage
{
    public int ImageId { get; set; }
    public int IssueId { get; set; }
    public byte Ordinal { get; set; }
    public string MimeType { get; set; } = null!;
    public string? FileName { get; set; }
    public int Size { get; set; }
    public byte[] Content { get; set; } = null!;
}

public static class IssueStatus
{
    public const string New = "NEW";
    public const string InProgress = "IN_PROGRESS";
    public const string Fixed = "FIXED";
    public const string Rejected = "REJECTED";
    public const string Duplicate = "DUPLICATE";

    public static readonly string[] All = [New, InProgress, Fixed, Rejected, Duplicate];
    public static readonly string[] Open = [New, InProgress];
}

public static class IssueType
{
    public const string Bug = "BUG";
    public const string Improvement = "IMPROVEMENT";
    public const string Question = "QUESTION";

    public static readonly string[] All = [Bug, Improvement, Question];
}
