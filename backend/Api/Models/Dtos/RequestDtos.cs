namespace TremblantLifecycle.Api.Models.Dtos;

/// <summary>POST /api/requests body — just enough to create the shell; the wizard fills in the
/// rest via PUT as the user progresses through steps.</summary>
public class CreateRequestDto
{
    public string RequestType { get; set; } = null!; // "Onboarding" | "Reactivation" | "Offboarding"
}

public class RequestEmployeeDto
{
    public string WorkdayEmployeeId { get; set; } = null!;
    public string NameSnapshot { get; set; } = null!;
    public string? PositionSnapshot { get; set; }
    public string? DepartementSnapshot { get; set; }
    public string? CodeEmploiSnapshot { get; set; }
    public string? TypeEmploiSnapshot { get; set; }
    public string? GestionnaireSnapshot { get; set; }
}

/// <summary>Full request shape for GET/PUT — mirrors src/types.ts's OnboardingRequest closely so
/// the frontend's mapping stays close to a 1:1 rename rather than a real transform.
/// CommentairesRH is included ONLY when RequestAuthorizationService.CanReadConfidentialCommentAsync
/// allows it for the calling user — see RequestsController.</summary>
public class RequestDto
{
    public int RequestId { get; set; }
    public string RequestNumber { get; set; } = null!;
    public string RequestType { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string DemandePar { get; set; } = null!;
    public DateTime CreatedAt { get; set; }

    public List<RequestEmployeeDto> Employees { get; set; } = [];

    // Onboarding/Réactivation
    public DateOnly? DateEntreePrevue { get; set; }
    public string? RegleDePaye { get; set; }
    public string? RegleDePayeCommentaire { get; set; }

    // Access
    public List<string> SystemesAcces { get; set; } = [];
    public string? BadgeZones { get; set; }
    public string? CodeAlarmeDetails { get; set; }
    public List<string> SystemePosHebergement { get; set; } = [];
    public string? StationnementRequis { get; set; }
    public string? JustificationAcces { get; set; }

    // Equipment
    public List<string> Equipements { get; set; } = [];
    public string? NotesEquipement { get; set; }

    // Applications
    public List<string> Applications { get; set; } = [];
    public string? AutreLogicielRequis { get; set; }

    // Offboarding
    public DateOnly? DerniereJournee { get; set; }
    public string? IndemniteVacances { get; set; }
    public string? RaisonArret { get; set; }
    public string? DetailsRaison { get; set; }
    public string? Reembaucheriez { get; set; }
    public string? CommentairesIT { get; set; }
    public string? CommentairesStationnement { get; set; }
    public string? CommentairesPuceAcces { get; set; }
    public string? CommentairesRedingote { get; set; }
    public string? DateRetourConnue { get; set; }
    public DateOnly? DateRetourTravail { get; set; }
    public string? PreavisRecu { get; set; }
    public string? MotifNonAdmissibilite { get; set; }

    /// <summary>Only populated when the caller passed the RequestAuthorizationService check —
    /// omitted entirely (not just null) from the JSON when access is denied, so its absence itself
    /// doesn't leak whether a comment exists. See RequestsController's serialization.</summary>
    public string? CommentairesRH { get; set; }
}

/// <summary>PUT /api/requests/{id} body — same shape as RequestDto minus server-owned fields
/// (RequestId/RequestNumber/Status/DemandePar/CreatedAt). Employees are replaced wholesale on each
/// PUT (simplest correct behavior for a multi-step wizard that can add/remove selections freely).</summary>
public class UpdateRequestDto
{
    public List<RequestEmployeeDto> Employees { get; set; } = [];
    public DateOnly? DateEntreePrevue { get; set; }
    public string? RegleDePaye { get; set; }
    public string? RegleDePayeCommentaire { get; set; }
    public List<string> SystemesAcces { get; set; } = [];
    public string? BadgeZones { get; set; }
    public string? CodeAlarmeDetails { get; set; }
    public List<string> SystemePosHebergement { get; set; } = [];
    public string? StationnementRequis { get; set; }
    public string? JustificationAcces { get; set; }
    public List<string> Equipements { get; set; } = [];
    public string? NotesEquipement { get; set; }
    public List<string> Applications { get; set; } = [];
    public string? AutreLogicielRequis { get; set; }
    public DateOnly? DerniereJournee { get; set; }
    public string? IndemniteVacances { get; set; }
    public string? RaisonArret { get; set; }
    public string? DetailsRaison { get; set; }
    public string? Reembaucheriez { get; set; }
    public string? CommentairesIT { get; set; }
    public string? CommentairesStationnement { get; set; }
    public string? CommentairesPuceAcces { get; set; }
    public string? CommentairesRedingote { get; set; }
    public string? DateRetourConnue { get; set; }
    public DateOnly? DateRetourTravail { get; set; }
    public string? PreavisRecu { get; set; }
    public string? MotifNonAdmissibilite { get; set; }

    /// <summary>Write-only from this DTO's perspective — the author can always write this as
    /// normal form entry; whether it can be READ BACK later is a separate check
    /// (RequestAuthorizationService), not enforced here.</summary>
    public string? CommentairesRH { get; set; }
}

public class MeDto
{
    public string ObjectId { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string? Email { get; set; }
    public bool IsHr { get; set; }
    public bool IsTicketTemplateAdmin { get; set; }
}

public class HelpUrlDto
{
    public string Url { get; set; } = null!;
}

public class CreateHelpTicketDto
{
    public string Description { get; set; } = null!;
}

public class HelpTicketResultDto
{
    public int TicketId { get; set; }
}
