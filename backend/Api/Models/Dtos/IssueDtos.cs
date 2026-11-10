namespace TremblantLifecycle.Api.Models.Dtos;

public record IssueImageDto(int ImageId, string MimeType);
public record IssueFileDto(int FileId, string FileName, string MimeType, int Size);

public class IssueReportDto
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
    public string? Context { get; set; }
    public string Status { get; set; } = null!;
    public string? Resolution { get; set; }
    public string? CommitRef { get; set; }
    public string? WorkLog { get; set; }
    public DateTime? HandledUtc { get; set; }
    public string? HandledBySam { get; set; }
    public List<IssueImageDto> Images { get; set; } = [];
    public List<IssueFileDto> Files { get; set; } = [];
    /// <summary>Fil « Échange » : nombre de messages, date du dernier, et s'il vient de l'équipe (une question attend
    /// alors la personne qui a signalé).</summary>
    public int MessageCount { get; set; }
    public DateTime? LastMessageUtc { get; set; }
    public bool LastMessageFromTeam { get; set; }
}

/// <summary>Un message du fil « Échange » ; FromTeam = écrit par l'équipe, sinon par la personne qui a signalé.</summary>
public record IssueMessageDto(int MessageId, DateTime CreatedUtc, bool FromTeam, string? AuthorSam, string? AuthorName, string Body);

/// <summary>Notify = aviser l'autre côté par courriel (au mieux). Vrai par défaut.</summary>
public record IssueMessageInput(string? Body, bool? Notify);

/// <summary>La réponse de la personne qui a signalé à un correctif livré (« À valider ») : réglé ou non, commentaire facultatif.</summary>
public record IssueConfirmInput(bool Regle, string? Commentaire);

public class IssueFileInput
{
    public string FileName { get; set; } = "";
    public string DataUrl { get; set; } = "";
}

public class IssueReportInput
{
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string? Expected { get; set; }
    public string? Screen { get; set; }
    public string? Context { get; set; }
    /// <summary>Pasted screenshots as "data:image/...;base64,..." URLs.</summary>
    public List<string> Captures { get; set; } = [];
    public List<IssueFileInput> Files { get; set; } = [];
}

public class IssueEditInput
{
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string? Expected { get; set; }
    public List<int> RemoveAttachmentIds { get; set; } = [];
    public List<string> Captures { get; set; } = [];
    public List<IssueFileInput> Files { get; set; } = [];
}

public class IssueUpdateInput
{
    public string Status { get; set; } = "";
    public string? Resolution { get; set; }
    public string? CommitRef { get; set; }
    public string? WorkLog { get; set; }
}

public class IssueCountDto
{
    public int Count { get; set; }
}
