using TremblantLifecycle.Api.Models.Dtos;
using TremblantLifecycle.Api.Models.Entities;

namespace TremblantLifecycle.Api.Services;

/// <summary>Maps a Request to the DTO the frontend renders. Shared by the requester-facing wizard
/// (RequestsController) and the Administration screen (AdminRequestsController) so both show the
/// same fields — a second copy would drift, and an admin reviewing a request needs to see exactly
/// what the requester entered, not an approximation of it.
///
/// CommentairesRH is deliberately NOT mapped here. It lives in a physically separate, access-
/// controlled table and every caller must decide, explicitly and visibly, whether the current user
/// may see it.</summary>
public static class RequestMapper
{
    public static RequestDto ToDto(Request r) => new()
    {
        RequestId = r.RequestId,
        RequestNumber = r.RequestNumber,
        RequestType = r.RequestType.ToString(),
        Status = r.Status.ToString(),
        DemandePar = r.CreatedByDisplayName,
        CreatedAt = r.CreatedAt,
        Employees = r.Employees.Select(e => new RequestEmployeeDto
        {
            WorkdayEmployeeId = e.WorkdayEmployeeId,
            NameSnapshot = e.NameSnapshot,
            PositionSnapshot = e.PositionSnapshot,
            DepartementSnapshot = e.DepartementSnapshot,
            CodeEmploiSnapshot = e.CodeEmploiSnapshot,
            TypeEmploiSnapshot = e.TypeEmploiSnapshot,
            GestionnaireSnapshot = e.GestionnaireSnapshot
        }).ToList(),
        DateEntreePrevue = r.OnboardingDetail?.DateEntreePrevue,
        RegleDePaye = r.OnboardingDetail?.RegleDePaye,
        RegleDePayeCommentaire = r.OnboardingDetail?.RegleDePayeCommentaire,
        SystemesAcces = r.AccessDetail?.Systemes.Select(s => s.Value).ToList() ?? [],
        BadgeZones = r.AccessDetail?.BadgeZones,
        CodeAlarmeDetails = r.AccessDetail?.CodeAlarmeDetails,
        SystemePosHebergement = r.AccessDetail?.PosHebergement.Select(p => p.Value).ToList() ?? [],
        StationnementRequis = r.AccessDetail?.Stationnement,
        JustificationAcces = r.AccessDetail?.Justification,
        Equipements = r.EquipmentDetail?.Equipements.Select(e => e.Value).ToList() ?? [],
        NotesEquipement = r.EquipmentDetail?.Notes,
        Applications = r.ApplicationsDetail?.Applications.Select(a => a.Value).ToList() ?? [],
        AutreLogicielRequis = r.ApplicationsDetail?.AutreLogiciel,
        DerniereJournee = r.OffboardingDetail?.DerniereJournee,
        IndemniteVacances = r.OffboardingDetail?.IndemniteVacances,
        RaisonArret = r.OffboardingDetail?.RaisonArret,
        DetailsRaison = r.OffboardingDetail?.DetailsRaison,
        Reembaucheriez = r.OffboardingDetail?.Reembaucheriez,
        CommentairesIT = r.OffboardingDetail?.CommentairesIT ?? r.OnboardingDetail?.CommentairesIT,
        CommentairesStationnement = r.OffboardingDetail?.CommentairesStationnement ?? r.OnboardingDetail?.CommentairesStationnement,
        CommentairesPuceAcces = r.OffboardingDetail?.CommentairesPuceAcces ?? r.OnboardingDetail?.CommentairesPuceAcces,
        CommentairesRedingote = r.OffboardingDetail?.CommentairesRedingote ?? r.OnboardingDetail?.CommentairesRedingote,
        DateRetourConnue = r.OffboardingDetail?.DateRetourConnue,
        DateRetourTravail = r.OffboardingDetail?.DateRetourTravail,
        PreavisRecu = r.OffboardingDetail?.PreavisRecu,
        MotifNonAdmissibilite = r.OffboardingDetail?.MotifNonAdmissibilite
        // CommentairesRH deliberately not mapped here — see GetInternal.
    };
}
