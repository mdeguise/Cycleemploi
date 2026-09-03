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

    /// <summary>Creates AND submits a request in one atomic call — there is no partial-save/draft
    /// state. The wizard collects everything client-side across its steps and only calls the
    /// server once, at final submission. Server-side validation (ValidateForSubmitAsync) runs
    /// BEFORE anything is added to the DbContext, so a validation failure leaves no row behind at
    /// all — unlike the old Create-then-PUT-then-Submit flow, a failed attempt here is invisible to
    /// everyone, including the requester's own abandoned browser tab.</summary>
    [HttpPost]
    public async Task<ActionResult<RequestDto>> Create(SubmitRequestDto dto, CancellationToken ct)
    {
        if (!Enum.TryParse<RequestType>(dto.RequestType, out var requestType))
        {
            return BadRequest($"Unknown requestType '{dto.RequestType}'.");
        }

        var now = DateTime.UtcNow;
        var requester = _ad.GetUserInfo(User.GetSamAccountName());
        var request = new Request
        {
            RequestType = requestType,
            Status = RequestStatus.Soumise,
            CreatedByObjectId = User.GetObjectId(),
            CreatedByDisplayName = requester.DisplayName ?? User.GetObjectId(),
            CreatedAt = now,
            UpdatedAt = now
        };

        request.Employees = dto.Employees.Select((e, i) => new RequestEmployee
        {
            WorkdayEmployeeId = e.WorkdayEmployeeId,
            NameSnapshot = e.NameSnapshot,
            PositionSnapshot = e.PositionSnapshot,
            DepartementSnapshot = e.DepartementSnapshot,
            CodeEmploiSnapshot = e.CodeEmploiSnapshot,
            TypeEmploiSnapshot = e.TypeEmploiSnapshot,
            GestionnaireSnapshot = e.GestionnaireSnapshot,
            IsPrimary = i == 0
        }).ToList();

        if (requestType is RequestType.Onboarding or RequestType.Reactivation)
        {
            request.OnboardingDetail = new OnboardingDetail
            {
                DateEntreePrevue = dto.DateEntreePrevue,
                RegleDePaye = dto.RegleDePaye,
                RegleDePayeCommentaire = dto.RegleDePayeCommentaire,
                CommentairesIT = dto.CommentairesIT,
                CommentairesStationnement = dto.CommentairesStationnement,
                CommentairesPuceAcces = dto.CommentairesPuceAcces,
                CommentairesRedingote = dto.CommentairesRedingote
            };

            if (dto.CommentairesRH is not null)
            {
                request.OnboardingConfidentialComment = new OnboardingConfidentialComment
                {
                    CommentaireRH = dto.CommentairesRH,
                    UpdatedAt = now,
                    UpdatedByObjectId = User.GetObjectId()
                };
            }
        }

        request.AccessDetail = new AccessDetail
        {
            BadgeZones = dto.BadgeZones,
            CodeAlarmeDetails = dto.CodeAlarmeDetails,
            Justification = dto.JustificationAcces,
            Stationnement = dto.StationnementRequis,
            Systemes = dto.SystemesAcces.Distinct().Select(v => new RequestAccessSysteme { Value = v }).ToList(),
            PosHebergement = dto.SystemePosHebergement.Distinct().Select(v => new RequestAccessPos { Value = v }).ToList()
        };

        request.EquipmentDetail = new EquipmentDetail
        {
            Notes = dto.NotesEquipement,
            Equipements = dto.Equipements.Distinct().Select(v => new RequestEquipment { Value = v }).ToList()
        };

        request.ApplicationsDetail = new ApplicationsDetail
        {
            AutreLogiciel = dto.AutreLogicielRequis,
            Applications = dto.Applications.Distinct().Select(v => new RequestApplication { Value = v }).ToList()
        };

        if (requestType == RequestType.Offboarding)
        {
            request.OffboardingDetail = new OffboardingDetail
            {
                DerniereJournee = dto.DerniereJournee,
                IndemniteVacances = dto.IndemniteVacances,
                RaisonArret = dto.RaisonArret,
                DetailsRaison = dto.DetailsRaison,
                Reembaucheriez = dto.Reembaucheriez,
                CommentairesIT = dto.CommentairesIT,
                CommentairesStationnement = dto.CommentairesStationnement,
                CommentairesPuceAcces = dto.CommentairesPuceAcces,
                CommentairesRedingote = dto.CommentairesRedingote,
                DateRetourConnue = dto.DateRetourConnue,
                DateRetourTravail = dto.DateRetourTravail,
                PreavisRecu = dto.PreavisRecu,
                MotifNonAdmissibilite = dto.MotifNonAdmissibilite
            };

            if (dto.CommentairesRH is not null)
            {
                request.ConfidentialComment = new OffboardingConfidentialComment
                {
                    CommentaireRH = dto.CommentairesRH,
                    UpdatedAt = now,
                    UpdatedByObjectId = User.GetObjectId()
                };
            }
        }

        // Server-side re-validation mirroring WizardContext.validateStep on the frontend — client
        // validation must never be trusted alone. Runs before the entity ever touches _db, so a
        // failure here leaves nothing behind.
        var errors = await ValidateForSubmitAsync(request, ct);
        if (errors.Count > 0)
        {
            return BadRequest(new { errors });
        }

        request.RequestNumber = await _requestNumbers.GenerateAsync(requestType, ct);
        request.SubmittedAt = now;

        // Captured now, while the caller IS the requester. Every downstream ticket is raised under
        // this address (in Freshdesk it drives the reply thread), and a retry is performed by an
        // administrator — so re-deriving it from the caller later would misattribute the ticket.
        request.RequesterEmail = requester.Email;

        _db.Requests.Add(request);
        await _db.SaveChangesAsync(ct);

        // Best-effort: the submission has already succeeded and is committed by this point.
        // Downstream ticket-system integrations (Freshdesk now, TDX later) must never be able to
        // fail a submission the requester already completed — a failure here notifies IT support by
        // email instead, so a ticket can be created manually. Freshdesk runs first so its ticket id
        // (if it succeeded) can be included in the D365 webhook payload for cross-referencing.
        await _orchestration.RunAllAsync(request, requester, ct);

        return Ok(MapToDto(request));
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

    private static RequestDto MapToDto(Request r) => RequestMapper.ToDto(r);
}
