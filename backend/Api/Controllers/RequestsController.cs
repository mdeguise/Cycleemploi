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
    private readonly IFreshdeskService _freshdesk;
    private readonly FreshdeskOptions _freshdeskOptions;
    private readonly IDynamicsEamService _dynamics;
    private readonly ITdxService _tdx;
    private readonly IEmailNotificationService _email;
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
        IFreshdeskService freshdesk,
        IOptions<FreshdeskOptions> freshdeskOptions,
        IDynamicsEamService dynamics,
        ITdxService tdx,
        IEmailNotificationService email,
        ILogger<RequestsController> logger)
    {
        _db = db;
        _workday = workday;
        _requestNumbers = requestNumbers;
        _authz = authz;
        _ad = ad;
        _freshdesk = freshdesk;
        _freshdeskOptions = freshdeskOptions.Value;
        _dynamics = dynamics;
        _tdx = tdx;
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
        await _db.SaveChangesAsync(ct);

        // Best-effort: the submission has already succeeded and is committed by this point.
        // Downstream ticket-system integrations (Freshdesk now, TDX later) must never be able to
        // fail a submission the requester already completed — a failure here notifies IT support by
        // email instead, so a ticket can be created manually. Freshdesk runs first so its ticket id
        // (if it succeeded) can be included in the D365 webhook payload for cross-referencing.
        var freshdeskTicketId = await TryCreateFreshdeskTicketAsync(request, ct);
        await TryCreateD365BadgeTicketAsync(request, freshdeskTicketId, ct);
        await TryCreateTdxTicketAsync(request, ct);
        await TryCreateD365AccessTicketAsync(request, ct);

        return NoContent();
    }

    private async Task<long?> TryCreateFreshdeskTicketAsync(Request request, CancellationToken ct)
    {
        string? requesterEmail = null;
        try
        {
            requesterEmail = _ad.GetUserInfo(User.GetSamAccountName()).Email;
            if (string.IsNullOrWhiteSpace(requesterEmail))
            {
                throw new InvalidOperationException("Could not resolve requester email from AD.");
            }

            var ticketId = await _freshdesk.CreateTicketAsync(request, requesterEmail, ct);

            // Best-effort, independent of each other and of the main ticket above (which already
            // succeeded and is committed) — fanning the same submission out to two other
            // departments as Freshdesk "child" tickets of the main one.
            await TryCreateFreshdeskChildTicketAsync(request, ticketId, requesterEmail, _freshdeskOptions.ChildGroupIdWithJobCodes, includeAllJobCodes: true, ct);
            await TryCreateFreshdeskChildTicketAsync(request, ticketId, requesterEmail, _freshdeskOptions.ChildGroupIdWithoutJobCodes, includeAllJobCodes: false, ct);

            return ticketId;
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

    /// <summary>Creates one Freshdesk "child" ticket (Parent-child ticketing feature) of the
    /// already-created main ticket, fanning the submission out to another department's group.
    /// Independent failure handling from the main ticket and from the other child ticket — either
    /// can fail without affecting the other, since the main ticket (and the submission itself) is
    /// already committed by the time either of these run.</summary>
    private async Task TryCreateFreshdeskChildTicketAsync(Request request, long parentTicketId, string requesterEmail, long groupId, bool includeAllJobCodes, CancellationToken ct)
    {
        try
        {
            await _freshdesk.CreateChildTicketAsync(request, parentTicketId, requesterEmail, groupId, includeAllJobCodes, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Freshdesk child ticket (group {GroupId}) for request {RequestNumber}", groupId, request.RequestNumber);

            var subject = $"[Cycle Emploi] Échec de création de ticket Freshdesk enfant (groupe {groupId}) — demande #{request.RequestNumber}";
            var body =
                $"La demande #{request.RequestNumber} ({request.RequestType.ToFrenchLabel()}) a été soumise avec succès, et le ticket Freshdesk principal (#{parentTicketId}) a été créé, " +
                $"mais la création du ticket enfant destiné au groupe {groupId} a échoué. Ce ticket devra être créé manuellement.\n\n" +
                "== Détails de la demande ==\n" +
                $"Demandé par: {request.CreatedByDisplayName}\n" +
                $"Ticket principal Freshdesk: #{parentTicketId}\n" +
                $"Date de soumission: {request.SubmittedAt:yyyy-MM-dd HH:mm} UTC\n\n" +
                "== Détails de l'erreur ==\n" +
                $"Type: {ex.GetType().Name}\n" +
                $"Message: {ex.Message}\n" +
                (ex.InnerException is not null ? $"Cause interne: {ex.InnerException.Message}\n" : "") +
                $"Survenue: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC\n\n" +
                "== Étapes à vérifier ==\n" +
                "1. Consulter les journaux applicatifs sur vm-trm-live pour la trace complète, en recherchant le numéro de demande ci-dessus.\n" +
                "2. Si l'erreur mentionne un code HTTP Freshdesk (401/403): la clé API a peut-être expiré ou été révoquée.\n" +
                "3. Si l'erreur mentionne \"parent_id\" ou un champ invalide: vérifier que la fonctionnalité \"Parent-child ticketing\" est toujours activée dans Freshdesk (Admin > Fonctionnalités avancées) et que le ticket principal existe toujours.\n" +
                $"4. Une fois la cause corrigée, créer le ticket manuellement dans Freshdesk (groupe {groupId}), en le liant comme ticket enfant du ticket principal #{parentTicketId} — les données complètes de la demande restent disponibles dans l'application Cycle Emploi.";

            try
            {
                await _email.SendAsync(subject, body, ct);
            }
            catch (Exception emailEx)
            {
                _logger.LogError(emailEx, "Also failed to send the Freshdesk-child-ticket-failure notification email for request {RequestNumber}, group {GroupId}", request.RequestNumber, groupId);
            }
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

    /// <summary>Creates a TDX ticket (TeamDynamix, "OneIT" app) for every submitted request,
    /// regardless of type — unlike the D365 badge/alarm integration, there's no gating condition
    /// here. Loops per employee on the request, same as the Freshdesk child tickets and D365
    /// integration, since a termination can target several people at once.</summary>
    private async Task TryCreateTdxTicketAsync(Request request, CancellationToken ct)
    {
        List<RequestEmployee> employeesToProcess = request.RequestType == RequestType.Offboarding
            ? request.Employees.ToList()
            : (request.Employees.FirstOrDefault(e => e.IsPrimary) ?? request.Employees.FirstOrDefault()) is { } emp
                ? [emp]
                : [];

        var requesterInfo = _ad.GetUserInfo(User.GetSamAccountName());
        var requesterName = requesterInfo.DisplayName ?? request.CreatedByDisplayName;
        var requesterEmail = requesterInfo.Email;

        foreach (var employee in employeesToProcess)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(requesterEmail))
                {
                    throw new InvalidOperationException("Could not resolve requester email from AD.");
                }

                var tdxTicketId = await _tdx.CreateTicketAsync(request, employee, requesterName, requesterEmail, ct);
                _logger.LogInformation("Created TDX ticket {TdxTicketId} for request {RequestNumber}, employee {EmployeeName}", tdxTicketId, request.RequestNumber, employee.NameSnapshot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create TDX ticket for request {RequestNumber}, employee {EmployeeName}", request.RequestNumber, employee.NameSnapshot);

                var subject = $"[Cycle Emploi] Échec de création de billet TDX — demande #{request.RequestNumber} — {employee.NameSnapshot}";
                var body =
                    $"La demande #{request.RequestNumber} ({request.RequestType.ToFrenchLabel()}) a été soumise avec succès, " +
                    $"mais la création du billet TDX correspondant à {employee.NameSnapshot} a échoué. Le billet devra être créé manuellement.\n\n" +
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
                    "2. Si l'erreur mentionne \"TDX username/password not configured\": l'intégration TDX n'a pas encore été configurée — voir la section Tdx de appsettings.Production.json.\n" +
                    "3. Si l'erreur mentionne un code HTTP 401 sur /api/auth: le mot de passe du compte de service TDX a peut-être expiré ou été changé.\n" +
                    "4. Si l'erreur mentionne \"returned no match\" sur la recherche du demandeur: le compte AD du demandeur n'a pas d'adresse courriel valide, ou cette adresse n'existe pas dans TDX — vérifier l'attribut mail dans Active Directory.\n" +
                    "5. Si l'erreur indique un problème de connexion/timeout: vérifier que vm-trm-live peut atteindre https://get.alterra.support (port 443).\n" +
                    "6. Une fois la cause corrigée, créer le billet manuellement dans TDX (application OneIT, formulaire Quick Incident, groupe IT Operations) avec les détails ci-dessus — les données complètes de la demande restent disponibles dans l'application Cycle Emploi.";

                try
                {
                    await _email.SendAsync(subject, body, ct);
                }
                catch (Exception emailEx)
                {
                    _logger.LogError(emailEx, "Also failed to send the TDX-failure notification email for request {RequestNumber}, employee {EmployeeName}", request.RequestNumber, employee.NameSnapshot);
                }
            }
        }
    }

    /// <summary>Creates a TDX ticket on the "D365 - Access" form (FormID 10799) when "Accès D365"
    /// was selected — Onboarding/Réactivation only, for the primary employee, same gating pattern as
    /// the badge/alarm D365 integration. Looks up the employee's job code in D365JobCodeTemplate for
    /// the roles/legal entity/approval limit/etc. answers; if no template has been filled out yet for
    /// that job code, fails open (email notifies IT to fill it in and create the ticket by hand)
    /// rather than blocking the submission or guessing at values.</summary>
    private async Task TryCreateD365AccessTicketAsync(Request request, CancellationToken ct)
    {
        if (request.RequestType == RequestType.Offboarding) return;

        var systemes = (request.AccessDetail?.Systemes.Select(s => s.Value) ?? []).ToList();
        if (!systemes.Contains(AccesD365SystemeValue)) return;

        var employee = request.Employees.FirstOrDefault(e => e.IsPrimary) ?? request.Employees.FirstOrDefault();
        if (employee is null) return;

        try
        {
            var requesterInfo = _ad.GetUserInfo(User.GetSamAccountName());
            var requesterName = requesterInfo.DisplayName ?? request.CreatedByDisplayName;
            var requesterEmail = requesterInfo.Email;
            if (string.IsNullOrWhiteSpace(requesterEmail))
            {
                throw new InvalidOperationException("Could not resolve requester email from AD.");
            }

            var workdayInfo = await _workday.WorkdayDemographics
                .Where(w => w.EmployeeId == employee.WorkdayEmployeeId && w.PrimaryJob == 1)
                .Select(w => new { w.JobCode, w.PositionTitle, w.WorkEmail, w.Email, w.ManagerId, w.Manager })
                .FirstOrDefaultAsync(ct);
            if (workdayInfo is null)
            {
                throw new InvalidOperationException($"No active Workday record found for employee {employee.WorkdayEmployeeId}.");
            }

            var jobCode = workdayInfo.JobCode ?? employee.CodeEmploiSnapshot;
            if (string.IsNullOrWhiteSpace(jobCode))
            {
                throw new InvalidOperationException("Employee has no job code — cannot look up their D365 access template.");
            }

            var template = await _db.D365JobCodeTemplates
                .Include(t => t.Roles)
                .FirstOrDefaultAsync(t => t.JobCode == jobCode, ct);
            if (template is null)
            {
                throw new InvalidOperationException(
                    $"No D365 access template has been filled out yet for job code {jobCode} — see Formulaires D365 par code d'emploi.");
            }

            var employeeEmail = workdayInfo.WorkEmail ?? workdayInfo.Email;
            if (string.IsNullOrWhiteSpace(employeeEmail))
            {
                throw new InvalidOperationException("Employee has no email address on file in Workday.");
            }

            var managerName = workdayInfo.Manager;
            if (!string.IsNullOrWhiteSpace(workdayInfo.ManagerId))
            {
                var manager = await _workday.WorkdayDemographics
                    .Where(w => w.EmployeeId == workdayInfo.ManagerId && w.PrimaryJob == 1)
                    .Select(w => new { w.FirstName, w.PreferredFirstName, w.LastName })
                    .FirstOrDefaultAsync(ct);
                if (manager is not null)
                {
                    managerName = $"{manager.PreferredFirstName ?? manager.FirstName} {manager.LastName}";
                }
            }

            var input = new D365AccessTicketInput(
                RequesterName: requesterName,
                RequesterEmail: requesterEmail,
                EmployeeName: employee.NameSnapshot,
                EmployeeEmail: employeeEmail,
                JobTitle: template.JobTitleEnglish,
                LegalEntity: template.LegalEntity,
                DepartmentNumber: template.DepartmentNumber,
                LevyEmployee: template.LevyEmployee,
                ManagerName: managerName,
                StartDate: request.OnboardingDetail?.DateEntreePrevue,
                Roles: template.Roles.Select(r => r.Role).ToList(),
                ApprovalLimit: template.ApprovalLimit,
                ApAccessDetails: template.ApAccessDetails,
                AdditionalLegalEntities: template.AdditionalLegalEntities);

            var ticketId = await _tdx.CreateD365AccessTicketAsync(input, ct);
            _logger.LogInformation("Created D365 Access TDX ticket {TicketId} for request {RequestNumber}, employee {EmployeeName}", ticketId, request.RequestNumber, employee.NameSnapshot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create D365 Access TDX ticket for request {RequestNumber}, employee {EmployeeName}", request.RequestNumber, employee.NameSnapshot);

            var subject = $"[Cycle Emploi] Échec de création de billet TDX D365 Access — demande #{request.RequestNumber} — {employee.NameSnapshot}";
            var body =
                $"La demande #{request.RequestNumber} ({request.RequestType.ToFrenchLabel()}) a été soumise avec succès, " +
                $"mais la création automatique du billet TDX « D365 - Access » pour {employee.NameSnapshot} a échoué. Le billet devra être créé manuellement.\n\n" +
                "== Détails de la demande ==\n" +
                $"Employé: {employee.NameSnapshot}\n" +
                $"Code d'emploi: {employee.CodeEmploiSnapshot}\n" +
                $"Demandé par: {request.CreatedByDisplayName}\n" +
                $"Date de soumission: {request.SubmittedAt:yyyy-MM-dd HH:mm} UTC\n\n" +
                "== Détails de l'erreur ==\n" +
                $"Type: {ex.GetType().Name}\n" +
                $"Message: {ex.Message}\n" +
                (ex.InnerException is not null ? $"Cause interne: {ex.InnerException.Message}\n" : "") +
                $"Survenue: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC\n\n" +
                "== Étapes à vérifier ==\n" +
                "1. Si l'erreur mentionne « No D365 access template has been filled out »: remplir le formulaire D365 pour ce code d'emploi dans Formulaires D365 par code d'emploi, puis créer le billet TDX manuellement pour cette demande (il ne sera pas recréé automatiquement après coup).\n" +
                "2. Si l'erreur mentionne « No active Workday record » ou « no email address »: vérifier les données Workday de l'employé.\n" +
                "3. Consulter les journaux applicatifs sur vm-trm-live pour la trace complète, en recherchant le numéro de demande ci-dessus.\n" +
                "4. Une fois la cause corrigée, créer le billet manuellement dans TDX (application OneIT, formulaire D365 - Access) avec les détails ci-dessus.";

            try
            {
                await _email.SendAsync(subject, body, ct);
            }
            catch (Exception emailEx)
            {
                _logger.LogError(emailEx, "Also failed to send the D365-Access-ticket-failure notification email for request {RequestNumber}, employee {EmployeeName}", request.RequestNumber, employee.NameSnapshot);
            }
        }
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
                    .Where(w => w.EmployeeId == primaryEmployee.WorkdayEmployeeId && w.PrimaryJob == 1)
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
