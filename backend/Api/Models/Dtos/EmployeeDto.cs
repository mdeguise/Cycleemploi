namespace TremblantLifecycle.Api.Models.Dtos;

/// <summary>Shape returned by employee search/lookup — mirrors src/types.ts's
/// EmployeeDirectoryEntry on the frontend. Sourced from WorkdayDemographic, filtered to
/// PrimaryJob == true and EmploymentStatus != "Terminated" (see WorkdayDemographic entity doc comment
/// for why not simply == "Active").</summary>
public class EmployeeDto
{
    public string EmployeeId { get; set; } = null!;
    public string Prenom { get; set; } = null!;
    public string Nom { get; set; } = null!;
    public string? Poste { get; set; }
    public string? Departement { get; set; }
    public string? CodeEmploi { get; set; }
    public string? TypeEmploi { get; set; }
    public string? Gestionnaire { get; set; }

    /// <summary>Drives the "Règle de paye" field's requirement on the Nouvelle intégration step —
    /// employees in "CAN Tremblant-Non Union" don't need one (see Step1Employee.tsx and
    /// RequestsController.ValidateForSubmit).</summary>
    public string? PayGroup { get; set; }
}
