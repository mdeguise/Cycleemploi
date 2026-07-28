namespace TremblantLifecycle.Api.Models.Entities;

/// <summary>1:1 with Request. Multi-select fields (systèmes d'accès, POS/hébergement) live in the
/// junction tables below rather than as columns, mirroring how the catalogs stay small/static
/// backend constants rather than lookup tables for MVP.</summary>
public class AccessDetail
{
    public int RequestId { get; set; }
    public Request Request { get; set; } = null!;

    public string? BadgeZones { get; set; }
    public string? Justification { get; set; }
    public string? Stationnement { get; set; }

    public ICollection<RequestAccessSysteme> Systemes { get; set; } = new List<RequestAccessSysteme>();
    public ICollection<RequestAccessPos> PosHebergement { get; set; } = new List<RequestAccessPos>();
}

/// <summary>Junction row per selected access system (e.g. "Compte AD/courriel"). Value is the
/// catalog's display text directly — catalogs are small/static backend constants for MVP, not a
/// separate lookup table (see API design in the plan).</summary>
public class RequestAccessSysteme
{
    public int RequestId { get; set; }
    public string Value { get; set; } = null!;
}

public class RequestAccessPos
{
    public int RequestId { get; set; }
    public string Value { get; set; } = null!;
}
