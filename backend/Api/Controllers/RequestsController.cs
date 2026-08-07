using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
    private readonly RequestNumberService _requestNumbers;
    private readonly RequestAuthorizationService _authz;
    private readonly IAdDirectoryService _ad;
    private readonly IFreshdeskService _freshdesk;
    private readonly IDynamicsEamService _dynamics;
    private readonly IEmailNotificationService _email;
    private readonly ILogger<RequestsController> _logger;

    /// <summary>Systèmes junction rows store the catalog's display text directly (see
    /// AccessDetail's doc comment) — these must match src/data/catalogs.ts's ACCES_BADGE and
    /// BESOIN_CODE_ALARME exactly.</summary>
    private const string AccesBadgeSystemeValue = "Badge d'accès aux édifices";
    private const string BesoinCodeAlarmeSystemeValue = "Besoin de code d'alarme";

    public RequestsController(
        AppDbContext db,
        RequestNumberService requestNumbers,
        RequestAuthorizationService authz,
        IAdDirectoryService ad,
        IFreshdeskService freshdesk,
        IDynamicsEamService dynamics,
        IEmailNotificationService email,
        ILogger<RequestsController> logger)
    {
        _db = db;
        _requestNumbers = requestNumbers;
        _authz = authz;
        _ad = ad;
        _freshdesk = freshdesk;
        _dynamics = dynamics;
        _email = email;
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
        // passes — never inferred from the presence of OffboardingConfidentialComment alone.
        if (request.ConfidentialComment is not null &&
            _authz.CanReadConfidentialComment(request, User.GetObjectId()))
        {
            dto.CommentairesRH = request.ConfidentialComment.CommentaireRH;
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
            request.OffboardingDetail.CommentairesParkingAcces = dto.CommentairesParkingAcces;
            request.OffboardingDetail.CommentairesRedingote = dto.CommentairesRedingote;

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
        var errors = ValidateForSubmit(request);
        if (errors.Count > 0)
        {
            return BadRequest(new { errors });
        }

        request.Status = RequestStatus.Soumise;
        request.SubmittedAt = DateTime.UtcNow;
        request.UpdatedAt = request.SubmittedAt.Value;
        await _db.SaveChangesAsync(ct);

        // Best-effort: the submission has already succeeded and is committed by this point.
        // Downstream ticket-system integrations (Freshdesk now, TDX later) must never be able to
        // fail a submission the requester already completed — a failure here notifies IT support by
        // email instead, so a ticket can be created manually. Freshdesk runs first so its ticket id
        // (if it succeeded) can be included in the D365 webhook payload for cross-referencing.
        var freshdeskTicketId = await TryCreateFreshdeskTicketAsync(request, ct);
        await TryCreateD365BadgeTicketAsync(request, freshdeskTicketId, ct);

        return NoContent();
    }

    private async Task<long?> TryCreateFreshdeskTicketAsync(Request request, CancellationToken ct)
    {
        try
        {
            var requesterEmail = _ad.GetUserInfo(User.GetSamAccountName()).Email;
            if (string.IsNullOrWhiteSpace(requesterEmail))
            {
                throw new InvalidOperationException("Could not resolve requester email from AD.");
            }

            return await _freshdesk.CreateTicketAsync(request, requesterEmail, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Freshdesk ticket for request {RequestNumber}", request.RequestNumber);

            var subject = $"[Cycle Emploi] Échec de création de ticket Freshdesk — demande #{request.RequestNumber}";
            var body =
                $"La demande #{request.RequestNumber} ({request.RequestType.ToFrenchLabel()}) a été soumise avec succès, " +
                "mais la création du ticket Freshdesk correspondant a échoué. Un ticket devra être créé manuellement.\n\n" +
                "== Détails de la demande ==\n" +
                $"Demandé par: {request.CreatedByDisplayName}\n" +
                $"Date de soumission: {request.SubmittedAt:yyyy-MM-dd HH:mm} UTC\n\n" +
                "== Détails de l'erreur ==\n" +
                $"Type: {ex.GetType().Name}\n" +
                $"Message: {ex.Message}\n" +
                (ex.InnerException is not null ? $"Cause interne: {ex.InnerException.Message}\n" : "") +
                $"Survenue: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC\n\n" +
                "== Étapes à vérifier ==\n" +
                "1. Consulter les journaux applicatifs sur vm-trm-live (Event Viewer, ou les logs ASP.NET Core du site TremblantOnboardingApi) pour la trace complète, en recherchant le numéro de demande ci-dessus.\n" +
                "2. Si l'erreur mentionne un code HTTP Freshdesk (401/403): la clé API dans appsettings.Production.json sur le serveur a peut-être expiré ou été révoquée — vérifier/régénérer dans Freshdesk (Profil > Paramètres > Clé API).\n" +
                "3. Si l'erreur mentionne \"Could not resolve requester email from AD\": le compte AD du demandeur n'a pas d'adresse courriel valide renseignée — vérifier l'attribut mail dans Active Directory.\n" +
                "4. Si l'erreur indique un problème de connexion/timeout: vérifier que vm-trm-live peut atteindre https://tremblantsmt.freshdesk.com (port 443) — pare-feu ou proxy sortant.\n" +
                "5. Si l'erreur mentionne group_id, email_config_id ou un champ invalide: la configuration Freshdesk (groupe \"RH - Général\", boîte courriel) a peut-être changé côté Freshdesk — comparer avec appsettings.json.\n" +
                "6. Une fois la cause corrigée, créer le ticket manuellement dans Freshdesk (groupe RH - Général) avec les détails de la demande ci-dessus — les données complètes de la demande restent disponibles dans l'application Cycle Emploi.";

            try
            {
                await _email.SendAsync(subject, body, ct);
            }
            catch (Exception emailEx)
            {
                // Nothing more useful to do here — the submission already succeeded and is
                // committed; let this surface in the server logs for someone to notice.
                _logger.LogError(emailEx, "Also failed to send the Freshdesk-failure notification email for request {RequestNumber}", request.RequestNumber);
            }

            return null;
        }
    }

    /// <summary>Creates a D365 F&amp;O Enterprise Asset Management work order for badge/alarm
    /// activation/deactivation, mirroring the old Freshservice-triggered Power Automate flow this
    /// replaces. For Onboarding/Réactivation, only fires when "Badge d'accès aux édifices" and/or
    /// "Besoin de code d'alarme" was selected, for the single employee on the request. For
    /// Offboarding there's no equivalent selection step, so it always fires — once per employee on
    /// the request, since a termination can target several people at once. Same fail-open,
    /// email-on-failure pattern as the Freshdesk integration above, applied per employee so one
    /// failure in a batch termination doesn't stop the others from being sent.</summary>
    private async Task TryCreateD365BadgeTicketAsync(Request request, long? freshdeskTicketId, CancellationToken ct)
    {
        List<RequestEmployee> employeesToProcess;
        if (request.RequestType == RequestType.Offboarding)
        {
            employeesToProcess = request.Employees.ToList();
        }
        else
        {
            var systemes = (request.AccessDetail?.Systemes.Select(s => s.Value) ?? []).ToList();
            if (!systemes.Contains(AccesBadgeSystemeValue) && !systemes.Contains(BesoinCodeAlarmeSystemeValue))
            {
                return;
            }

            var employee = request.Employees.FirstOrDefault(e => e.IsPrimary) ?? request.Employees.FirstOrDefault();
            employeesToProcess = employee is null ? [] : [employee];
        }

        foreach (var employee in employeesToProcess)
        {
            try
            {
                var d365JobCode = await _dynamics.CreateBadgeRequestAsync(request, employee, freshdeskTicketId, ct);
                _logger.LogInformation("Created D365 EAM badge request, jobcode {D365JobCode}, for request {RequestNumber}, employee {EmployeeName}", d365JobCode, request.RequestNumber, employee.NameSnapshot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create D365 EAM badge request for request {RequestNumber}, employee {EmployeeName}", request.RequestNumber, employee.NameSnapshot);

                var subject = $"[Cycle Emploi] Échec de création de billet D365 (badge/alarme) — demande #{request.RequestNumber} — {employee.NameSnapshot}";
                var body =
                    $"La demande #{request.RequestNumber} ({request.RequestType.ToFrenchLabel()}) a été soumise avec succès, " +
                    $"mais la création du billet D365 (Enterprise Asset Management) correspondant à {employee.NameSnapshot} a échoué. Le billet devra être créé manuellement.\n\n" +
                    "== Détails de la demande ==\n" +
                    $"Employé: {employee.NameSnapshot}\n" +
                    $"Demandé par: {request.CreatedByDisplayName}\n" +
                    $"Date de soumission: {request.SubmittedAt:yyyy-MM-dd HH:mm} UTC\n\n" +
                    "== Détails de l'erreur ==\n" +
                    $"Type: {ex.GetType().Name}\n" +
                    $"Message: {ex.Message}\n" +
                    (ex.InnerException is not null ? $"Cause interne: {ex.InnerException.Message}\n" : "") +
                    $"Survenue: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC\n\n" +
                    "== Étapes à vérifier ==\n" +
                    "1. Consulter les journaux applicatifs sur vm-trm-live pour la trace complète, en recherchant le numéro de demande ci-dessus.\n" +
                    "2. Si l'erreur mentionne \"webhook URL not configured\": l'intégration Power Automate n'a pas encore été configurée — voir la section PowerAutomate de appsettings.Production.json.\n" +
                    "3. Si l'erreur mentionne un code HTTP 4xx/5xx du webhook: vérifier dans Power Automate (Mes flux) que le flux qui crée les billets D365 (badge/alarme) est toujours activé et n'a pas d'erreur de connexion (ex. connexion Dynamics expirée).\n" +
                    "4. Si l'erreur indique un problème de connexion/timeout: vérifier que vm-trm-live peut atteindre prod-*.logic.azure.com (les points de terminaison Power Automate, port 443).\n" +
                    "5. Consulter l'historique d'exécution du flux dans Power Automate pour voir si la demande a été reçue et où elle a échoué côté D365.\n" +
                    "6. Une fois la cause corrigée, créer le billet manuellement dans D365 (Enterprise Asset Management, emplacement fonctionnel BF-SEC-GEN) avec les détails ci-dessus — les données complètes de la demande restent disponibles dans l'application Cycle Emploi.";

                try
                {
                    await _email.SendAsync(subject, body, ct);
                }
                catch (Exception emailEx)
                {
                    _logger.LogError(emailEx, "Also failed to send the D365-failure notification email for request {RequestNumber}, employee {EmployeeName}", request.RequestNumber, employee.NameSnapshot);
                }
            }
        }
    }

    private static List<string> ValidateForSubmit(Request request)
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
            if (string.IsNullOrWhiteSpace(request.OnboardingDetail?.RegleDePaye))
            {
                errors.Add("RegleDePaye is required.");
            }
            else if (request.OnboardingDetail.RegleDePaye == "AUTRES PRÉCISÉ DANS COMMENTAIRES" &&
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
        CommentairesIT = r.OffboardingDetail?.CommentairesIT,
        CommentairesParkingAcces = r.OffboardingDetail?.CommentairesParkingAcces,
        CommentairesRedingote = r.OffboardingDetail?.CommentairesRedingote
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
