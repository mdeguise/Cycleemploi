namespace TremblantLifecycle.Api.Models.Dtos;

/// <summary>Shape returned by employee search/lookup — mirrors src/types.ts's
/// EmployeeDirectoryEntry on the frontend. Sourced from WorkdayDemographic, filtered to
/// PrimaryJob == 1 and EmploymentStatus != "Terminated" (see WorkdayDemographic entity doc comment
/// for why not simply == "Active").</summary>
public class EmployeeDto
{
    public int EmployeeId { get; set; }
    public string Prenom { get; set; } = null!;
    public string Nom { get; set; } = null!;
    public string? Poste { get; set; }
    public string? Departement { get; set; }
    public string? CodeEmploi { get; set; }
    public string? TypeEmploi { get; set; }
    public string? Gestionnaire { get; set; }
}
