using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TremblantLifecycle.Api.Data;
using TremblantLifecycle.Api.Models.Dtos;
using TremblantLifecycle.Api.Models.Entities;
using TremblantLifecycle.Api.Services;

namespace TremblantLifecycle.Api.Controllers;

[ApiController]
[Route("api/requests")]
[Authorize]
public class RequestsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly WorkdayContext _workday;
    private readonly RequestNumberService _requestNumbers;
    private readonly RequestAuthorizationService _authz;
    private readonly IAdDirectoryService _ad;
    private readonly ITicketOrchestrationService _orchestration;
    private readonly ILogger<RequestsController> _logger;

    /// <summary>Systèmes junction rows store the catalog's display text directly (see
    /// AccessDetail's doc comment) — these must match src/data/catalogs.ts's ACCES_BADGE,
    /// BESOIN_CODE_ALARME and ACCES_D365 exactly.</summary>
    private const string AccesBadgeSystemeValue = "Badge d'accès aux édifices";
    private const string BesoinCodeAlarmeSystemeValue = "Besoin de code d'alarme";
    private const string AccesD365SystemeValue = "Accès D365";

    /// <summary>Employees in this Workday Pay_Group don't need to answer "Règle de paye" on the
    /// Nouvelle intégration/Réactivation step — mirrored on the frontend in
    /// src/data/catalogs.ts's PAY_GROUP_NON_UNION.</summary>
    private const string PayGroupNonUnion = "CAN Tremblant-Non Union";

    public RequestsController(
        AppDbContext db,
        WorkdayContext workday,
        RequestNumberService requestNumbers,
        RequestAuthorizationService authz,
        IAdDirectoryService ad,
        ITicketOrchestrationService orchestration,
        ILogger<RequestsController> logger)
    {
        _db = db;
        _workday = workday;
        _requestNumbers = requestNumbers;
        _authz = authz;
        _ad = ad;
        _orchestration = orchestration;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<RequestDto>> Create(CreateRequestDto dto, CancellationToken ct)
    {
        if (!Enum.TryParse<RequestType>(dto.RequestType, out var requestType))
        {
            return BadRequest($"Unknown requestType '{dto.RequestType}'.");
        }

        var now = DateTime.UtcNow;
        var request = new Request
        {
            RequestNumber = await _requestNumbers.GenerateAsync(requestType, ct),
            RequestType = requestType,
            Status = RequestStatus.Brouillon,
            CreatedByObjectId = User.GetObjectId(),
            CreatedByDisplayName = _ad.GetUserInfo(User.GetSamAccountName()).DisplayName ?? User.GetObjectId(),
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Requests.Add(request);
        await _db.SaveChangesAsync(ct);

        return await GetInternal(request.RequestId, ct);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<RequestDto>> Get(int id, CancellationToken ct) => await GetInternal(id, ct);

    private async Task<ActionResult<RequestDto>> GetInternal(int id, CancellationToken ct)
    {
        var request = await LoadFullRequestAsync(id, ct);
        if (request is null) return NotFound();

        var dto = MapToDto(request);

        // RH comment is loaded separately and only attached to the DTO if the authorization check
        // passes — never inferred from the presence of OffboardingConfidentialComment/
        // OnboardingConfidentialComment alone. A request only ever has one of the two.
        if (_authz.CanReadConfidentialComment(request, User.GetObjectId()))
        {
            dto.CommentairesRH = request.ConfidentialComment?.CommentaireRH ?? request.OnboardingConfidentialComment?.CommentaireRH;
        }

        return Ok(dto);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateRequestDto dto, CancellationToken ct)
    {
        var request = await LoadFullRequestAsync(id, ct);
        if (request is null) return NotFound();

        if (request.Status != RequestStatus.Brouillon)
        {
            return Conflict("Only draft requests can be updated.");
        }

        // Employees: replace wholesale — simplest correct behavior for a wizard that can freely
        // add/remove selections across steps.
        _db.RequestEmployees.RemoveRange(request.Employees);
        request.Employees = dto.Employees.Select((e, i) => new RequestEmployee
        {
            RequestId = id,
            WorkdayEmployeeId = e.WorkdayEmployeeId,
            NameSnapshot = e.NameSnapshot,
            PositionSnapshot = e.PositionSnapshot,
            DepartementSnapshot = e.DepartementSnapshot,
            CodeEmploiSnapshot = e.CodeEmploiSnapshot,
            TypeEmploiSnapshot = e.TypeEmploiSnapshot,
            GestionnaireSnapshot = e.GestionnaireSnapshot,
            IsPrimary = i == 0
        }).ToList();

        if (request.RequestType is RequestType.Onboarding or RequestType.Reactivation)
        {
            request.OnboardingDetail ??= new OnboardingDetail { RequestId = id };
            request.OnboardingDetail.DateEntreePrevue = dto.DateEntreePrevue ?? request.OnboardingDetail.DateEntreePrevue;
            request.OnboardingDetail.RegleDePaye = dto.RegleDePaye ?? request.OnboardingDetail.RegleDePaye;
            request.OnboardingDetail.RegleDePayeCommentaire = dto.RegleDePayeCommentaire;
            request.OnboardingDetail.CommentairesIT = dto.CommentairesIT;
            request.OnboardingDetail.CommentairesStationnement = dto.CommentairesStationnement;
            request.OnboardingDetail.CommentairesPuceAcces = dto.CommentairesPuceAcces;
            request.OnboardingDetail.CommentairesRedingote = dto.CommentairesRedingote;

            // Written unconditionally here — the author can always write this as normal form entry.
            // Whether it can be READ BACK is a separate, later check (RequestAuthorizationService),
            // never enforced on write. Mirrors the offboarding branch below.
            if (dto.CommentairesRH is not null)
            {
                request.OnboardingConfidentialComment ??= new OnboardingConfidentialComment { RequestId = id };
                request.OnboardingConfidentialComment.CommentaireRH = dto.CommentairesRH;
                request.OnboardingConfidentialComment.UpdatedAt = DateTime.UtcNow;
                request.OnboardingConfidentialComment.UpdatedByObjectId = User.GetObjectId();
            }
        }

        request.AccessDetail ??= new AccessDetail { RequestId = id };
        request.AccessDetail.BadgeZones = dto.BadgeZones;
        request.AccessDetail.CodeAlarmeDetails = dto.CodeAlarmeDetails;
        request.AccessDetail.Justification = dto.JustificationAcces;
        request.AccessDetail.Stationnement = dto.StationnementRequis;
        ReplaceJunction(_db.RequestAccessSystemes, request.AccessDetail.Systemes, id, dto.SystemesAcces,
            v => new RequestAccessSysteme { RequestId = id, Value = v });
        ReplaceJunction(_db.RequestAccessPos, request.AccessDetail.PosHebergement, id, dto.SystemePosHebergement,
            v => new RequestAccessPos { RequestId = id, Value = v });

        request.EquipmentDetail ??= new EquipmentDetail { RequestId = id };
        request.EquipmentDetail.Notes = dto.NotesEquipement;
        ReplaceJunction(_db.RequestEquipments, request.EquipmentDetail.Equipements, id, dto.Equipements,
            v => new RequestEquipment { RequestId = id, Value = v });

        request.ApplicationsDetail ??= new ApplicationsDetail { RequestId = id };
        request.ApplicationsDetail.AutreLogiciel = dto.AutreLogicielRequis;
        ReplaceJunction(_db.RequestApplications, request.ApplicationsDetail.Applications, id, dto.Applications,
            v => new RequestApplication { RequestId = id, Value = v });

        if (request.RequestType == RequestType.Offboarding)
        {
            request.OffboardingDetail ??= new OffboardingDetail { RequestId = id };
            request.OffboardingDetail.DerniereJournee = dto.DerniereJournee ?? request.OffboardingDetail.DerniereJournee;
            request.OffboardingDetail.IndemniteVacances = dto.IndemniteVacances ?? request.OffboardingDetail.IndemniteVacances;
            request.OffboardingDetail.RaisonArret = dto.RaisonArret ?? request.OffboardingDetail.RaisonArret;
            request.OffboardingDetail.DetailsRaison = dto.DetailsRaison ?? request.OffboardingDetail.DetailsRaison;
            request.OffboardingDetail.Reembaucheriez = dto.Reembaucheriez ?? request.OffboardingDetail.Reembaucheriez;
            request.OffboardingDetail.CommentairesIT = dto.CommentairesIT;
            request.OffboardingDetail.CommentairesStationnement = dto.CommentairesStationnement;
            request.OffboardingDetail.CommentairesPuceAcces = dto.CommentairesPuceAcces;
            request.OffboardingDetail.CommentairesRedingote = dto.CommentairesRedingote;
            request.OffboardingDetail.DateRetourConnue = dto.DateRetourConnue;
            request.OffboardingDetail.DateRetourTravail = dto.DateRetourTravail;
            request.OffboardingDetail.PreavisRecu = dto.PreavisRecu;
            request.OffboardingDetail.MotifNonAdmissibilite = dto.MotifNonAdmissibilite;

            // Written unconditionally here — the author can always write this as normal form entry.
            // Whether it can be READ BACK is a separate, later check (RequestAuthorizationService),
            // never enforced on write.
            if (dto.CommentairesRH is not null)
            {
                request.ConfidentialComment ??= new OffboardingConfidentialComment { RequestId = id };
                request.ConfidentialComment.CommentaireRH = dto.CommentairesRH;
                request.ConfidentialComment.UpdatedAt = DateTime.UtcNow;
                request.ConfidentialComment.UpdatedByObjectId = User.GetObjectId();
            }
        }

        request.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    [HttpPost("{id:int}/submit")]
    public async Task<IActionResult> Submit(int id, CancellationToken ct)
    {
        var request = await LoadFullRequestAsync(id, ct);
        if (request is null) return NotFound();

        if (request.Status != RequestStatus.Brouillon)
        {
            return Conflict("Request is not in a submittable state.");
        }

        // Server-side re-validation mirroring WizardContext.validateStep on the frontend — client
        // validation must never be trusted alone. See the plan's API design notes.
        var errors = await ValidateForSubmitAsync(request, ct);
        if (errors.Count > 0)
        {
            return BadRequest(new { errors });
        }

        request.Status = RequestStatus.Soumise;
        request.SubmittedAt = DateTime.UtcNow;
        request.UpdatedAt = request.SubmittedAt.Value;

        // Captured now, while the caller IS the requester. Every downstream ticket is raised under
        // this address (in Freshdesk it drives the reply thread), and a retry is performed by an
        // administrator — so re-deriving it from the caller later would misattribute the ticket.
        var requester = _ad.GetUserInfo(User.GetSamAccountName());
        request.RequesterEmail = requester.Email;

        await _db.SaveChangesAsync(ct);

        // Best-effort: the submission has already succeeded and is committed by this point.
        // Downstream ticket-system integrations (Freshdesk now, TDX later) must never be able to
        // fail a submission the requester already completed — a failure here notifies IT support by
        // email instead, so a ticket can be created manually. Freshdesk runs first so its ticket id
        // (if it succeeded) can be included in the D365 webhook payload for cross-referencing.
        await _orchestration.RunAllAsync(request, requester, ct);

        return NoContent();
    }

    private async Task<List<string>> ValidateForSubmitAsync(Request request, CancellationToken ct)
    {
        var errors = new List<string>();

        if (request.Employees.Count == 0)
        {
            errors.Add("At least one employee is required.");
        }

        if (request.RequestType is RequestType.Onboarding or RequestType.Reactivation)
        {
            if (request.OnboardingDetail is null || request.OnboardingDetail.DateEntreePrevue == default)
            {
                errors.Add("DateEntreePrevue is required.");
            }

            // Not required for CAN Tremblant-Non Union employees (see PayGroupNonUnion) — looked
            // up live rather than trusting a client-supplied flag, same reasoning as every other
            // check here: client validation must never be trusted alone.
            var primaryEmployee = request.Employees.FirstOrDefault(e => e.IsPrimary) ?? request.Employees.FirstOrDefault();
            var payGroup = primaryEmployee is null
                ? null
                : await _workday.WorkdayDemographics
                    .Where(w => w.EmployeeId == primaryEmployee.WorkdayEmployeeId && w.PrimaryJob == true)
                    .Select(w => w.PayGroup)
                    .FirstOrDefaultAsync(ct);
            var regleDePayeRequired = payGroup != PayGroupNonUnion;

            if (regleDePayeRequired && string.IsNullOrWhiteSpace(request.OnboardingDetail?.RegleDePaye))
            {
                errors.Add("RegleDePaye is required.");
            }
            else if (request.OnboardingDetail?.RegleDePaye == "AUTRES PRÉCISÉ DANS COMMENTAIRES" &&
                     string.IsNullOrWhiteSpace(request.OnboardingDetail.RegleDePayeCommentaire))
            {
                errors.Add("RegleDePayeCommentaire is required when RegleDePaye is 'AUTRES...'.");
            }
        }

        if (request.RequestType == RequestType.Offboarding)
        {
            var d = request.OffboardingDetail;
            if (d is null || d.DerniereJournee == default) errors.Add("DerniereJournee is required.");
            if (string.IsNullOrWhiteSpace(d?.IndemniteVacances)) errors.Add("IndemniteVacances is required.");
            if (string.IsNullOrWhiteSpace(d?.RaisonArret)) errors.Add("RaisonArret is required.");
            if (string.IsNullOrWhiteSpace(d?.DetailsRaison)) errors.Add("DetailsRaison is required.");
            if (string.IsNullOrWhiteSpace(d?.Reembaucheriez)) errors.Add("Reembaucheriez is required.");
        }

        return errors;
    }

    private async Task<Request?> LoadFullRequestAsync(int id, CancellationToken ct) =>
        await _db.Requests
            .Include(r => r.Employees)
            .Include(r => r.OnboardingDetail)
            .Include(r => r.AccessDetail).ThenInclude(a => a!.Systemes)
            .Include(r => r.AccessDetail).ThenInclude(a => a!.PosHebergement)
            .Include(r => r.EquipmentDetail).ThenInclude(e => e!.Equipements)
            .Include(r => r.ApplicationsDetail).ThenInclude(a => a!.Applications)
            .Include(r => r.OffboardingDetail)
            .Include(r => r.ConfidentialComment)
            .Include(r => r.OnboardingConfidentialComment)
            .FirstOrDefaultAsync(r => r.RequestId == id, ct);

    private static RequestDto MapToDto(Request r) => new()
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

    private static void ReplaceJunction<T>(
        Microsoft.EntityFrameworkCore.DbSet<T> dbSet,
        ICollection<T> current,
        int requestId,
        List<string> newValues,
        Func<string, T> factory) where T : class
    {
        dbSet.RemoveRange(current);
        current.Clear();
        foreach (var v in newValues.Distinct())
        {
            current.Add(factory(v));
        }
    }
}
