namespace TremblantLifecycle.Api.Models.Entities;

/// <summary>Phase 2 — file metadata only; actual bytes live on disk (filesystem/bind mount), not in
/// SQL varbinary, per the plan's Docker/deployment design.</summary>
public class Attachment
{
    public int AttachmentId { get; set; }
    public int RequestId { get; set; }
    public Request Request { get; set; } = null!;

    public string FileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long SizeBytes { get; set; }
    public string StoragePath { get; set; } = null!;
    public DateTime UploadedAt { get; set; }
    public string? UploadedByObjectId { get; set; }
}
