namespace TremblantLifecycle.Api.Models.Dtos;

/// <summary>Static reference data, mirroring src/data/catalogs.ts. Served from backend constants
/// for MVP (see CatalogsController) rather than DB-backed lookup tables — matches the plan's "start
/// as backend constants, move to DB only if the business needs to edit without a deploy" decision.</summary>
public class CatalogsDto
{
    public List<string> Departements { get; set; } = [];
    public List<string> TypesEmploi { get; set; } = [];
    public List<string> ReglesDePaye { get; set; } = [];
    public string RegleDePayeAutre { get; set; } = null!;
    public List<AccessSystemDto> SystemesAcces { get; set; } = [];
    public List<string> PosHebergementSystemes { get; set; } = [];
    public List<EquipmentItemDto> Equipements { get; set; } = [];
    public List<ApplicationItemDto> Applications { get; set; } = [];
    public List<string> OuiNon { get; set; } = [];
    public List<string> RaisonsArret { get; set; } = [];
    public List<string> ReembaucheriezOptions { get; set; } = [];
}

public class AccessSystemDto
{
    public string Nom { get; set; } = null!;
    public string Description { get; set; } = null!;
}

public class EquipmentItemDto
{
    public string Nom { get; set; } = null!;
    public string Categorie { get; set; } = null!;
}

public class ApplicationItemDto
{
    public string Nom { get; set; } = null!;
    public string Editeur { get; set; } = null!;
}
