using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TremblantLifecycle.Api.Data;
using TremblantLifecycle.Api.Models.Entities;

namespace TremblantLifecycle.Api.Services;

/// <summary>The outcome of retrying one ticket.</summary>
public record TicketRetryResult(bool Succeeded, string? TicketNumber, string? Error);

public interface ITicketOrchestrationService
{
    /// <summary>Fires every downstream integration for a freshly submitted request. Best-effort by
    /// design: the submission is already committed by the time this runs and must never be failed
    /// by a downstream system being unavailable.</summary>
    Task RunAllAsync(Request request, AdUserInfo requester, CancellationToken ct);

    /// <summary>Re-runs the ONE integration a RequestTicket row represents. Used by the
    /// Administration screen's Réessayer button.</summary>
    Task<TicketRetryResult> RetryAsync(Request request, RequestTicket ticket, CancellationToken ct);

    /// <summary>Builds and sends the real TDX "D365 - Access" ticket from a completed approval's
    /// saved fields — called by D365AccessApprovalsController.Complete right after an approver
    /// submits the form.</summary>
    Task<TicketRetryResult> CreateD365AccessTicketAsync(Request request, D365AccessApproval approval, CancellationToken ct);

    /// <summary>Emails the matched D365Approvers (or IT, if none match) that a fully-filled-out
    /// ad-hoc D365 access request is ready for their review — called by
    /// D365AccessApprovalsController.SubmitAdHoc right after it creates the Pending approval.</summary>
    Task NotifyD365ApproversOfAdHocRequestAsync(Request request, D365AccessApproval approval, string? positionTitle, CancellationToken ct);
}

/// <summary>All the downstream ticket-system integrations, extracted out of RequestsController so
/// the SAME code runs on submit and on retry — a second copy for retry would drift, and drift here
/// means tickets whose contents differ depending on how they were created.
///
/// The requester is passed IN rather than read from the HTTP caller. At submit time the caller is
/// the requester; on a retry the caller is an administrator, and re-deriving it there would raise
/// the ticket under the wrong person — in Freshdesk the requester drives the whole reply thread.
/// See Request.RequesterEmail.</summary>
public class TicketOrchestrationService : ITicketOrchestrationService
{
    private readonly AppDbContext _db;
    private readonly WorkdayContext _workday;
    private readonly IAdDirectoryService _ad;
    private readonly IFreshdeskService _freshdesk;
    private readonly FreshdeskOptions _freshdeskOptions;
    private readonly IDynamicsEamService _dynamics;
    private readonly ITdxService _tdx;
    private readonly IEmailNotificationService _email;
    private readonly IRequestTicketService _tickets;
    private readonly ID365ApproverService _d365Approvers;
    private readonly AppOptions _appOptions;
    private readonly ILogger<TicketOrchestrationService> _logger;

    /// <summary>Systèmes junction rows store the catalog's display text directly — these must match
    /// src/data/catalogs.ts's ACCES_BADGE, BESOIN_CODE_ALARME and ACCES_D365 exactly.</summary>
    private const string AccesBadgeSystemeValue = "Badge d'accès aux édifices";
    private const string BesoinCodeAlarmeSystemeValue = "Besoin de code d'alarme";
    private const string AccesD365SystemeValue = "Accès D365";

    public TicketOrchestrationService(
        AppDbContext db,
        WorkdayContext workday,
        IAdDirectoryService ad,
        IFreshdeskService freshdesk,
        IOptions<FreshdeskOptions> freshdeskOptions,
        IDynamicsEamService dynamics,
        ITdxService tdx,
        IEmailNotificationService email,
        IRequestTicketService tickets,
        ID365ApproverService d365Approvers,
        IOptions<AppOptions> appOptions,
        ILogger<TicketOrchestrationService> logger)
    {
        _db = db;
        _workday = workday;
        _ad = ad;
        _freshdesk = freshdesk;
        _freshdeskOptions = freshdeskOptions.Value;
        _dynamics = dynamics;
        _tdx = tdx;
        _email = email;
        _tickets = tickets;
        _d365Approvers = d365Approvers;
        _appOptions = appOptions.Value;
        _logger = logger;
    }

    public async Task RunAllAsync(Request request, AdUserInfo requester, CancellationToken ct)
    {
        // Freshdesk runs first so its ticket id (when it succeeded) can be included in the D365
        // webhook payload for cross-referencing.
        var freshdeskTicketId = await TryCreateFreshdeskTicketAsync(request, requester, ct);
        await TryCreateD365BadgeTicketAsync(request, freshdeskTicketId, ct);
        await TryCreateTdxTicketAsync(request, requester, ct);
        await TryCreateD365AccessApprovalRequestAsync(request, ct);
    }


    public async Task<TicketRetryResult> RetryAsync(Request request, RequestTicket ticket, CancellationToken ct)
    {
        // The requester is the person who SUBMITTED the request, never the administrator clicking
        // Réessayer. Re-deriving it from the caller here is the whole reason RequesterEmail is
        // persisted — see Request.RequesterEmail.
        var requester = new AdUserInfo(request.CreatedByDisplayName, request.RequesterEmail);
        if (string.IsNullOrWhiteSpace(requester.Email))
        {
            return new TicketRetryResult(false, null,
                "Cette demande n'a pas de courriel de demandeur enregistré (elle a été soumise avant que ce champ existe). " +
                "Le billet doit être créé manuellement.");
        }

        try
        {
            switch (ticket.Kind)
            {
                case TicketKind.Freshdesk:
                    // Also creates whichever child tickets never got made — they are only attempted
                    // after the parent succeeds, so a failed parent means no child rows exist at all
                    // and there would be nothing for an administrator to click.
                    await TryCreateFreshdeskTicketAsync(request, requester, ct);
                    break;

                case TicketKind.FreshdeskChildWithJobCodes:
                case TicketKind.FreshdeskChildWithoutJobCodes:
                {
                    var parent = await _db.RequestTickets.AsNoTracking()
                        .FirstOrDefaultAsync(t => t.RequestId == request.RequestId && t.Kind == TicketKind.Freshdesk, ct);
                    if (parent is null || parent.Outcome != TicketOutcome.Created || !long.TryParse(parent.TicketNumber, out var parentTicketId))
                    {
                        return new TicketRetryResult(false, null,
                            "Le billet Freshdesk principal n'existe pas encore — relancez-le d'abord; ses billets enfants suivront.");
                    }

                    var withJobCodes = ticket.Kind == TicketKind.FreshdeskChildWithJobCodes;
                    var groupId = withJobCodes ? _freshdeskOptions.ChildGroupIdWithJobCodes : _freshdeskOptions.ChildGroupIdWithoutJobCodes;
                    await TryCreateFreshdeskChildTicketAsync(request, parentTicketId, requester.Email!, groupId, withJobCodes, ticket.Kind, ct);
                    break;
                }

                case TicketKind.Tdx:
                    await TryCreateTdxTicketAsync(request, requester, ct, ticket.RequestEmployeeId);
                    break;

                case TicketKind.D365Badge:
                {
                    // Pass the Freshdesk id along when we have one, same as the submit path does, so
                    // the D365 record still cross-references the ticket.
                    var freshdesk = await _db.RequestTickets.AsNoTracking()
                        .FirstOrDefaultAsync(t => t.RequestId == request.RequestId && t.Kind == TicketKind.Freshdesk, ct);
                    long? freshdeskTicketId = freshdesk is { Outcome: TicketOutcome.Created } && long.TryParse(freshdesk.TicketNumber, out var fdId)
                        ? fdId
                        : null;
                    await TryCreateD365BadgeTicketAsync(request, freshdeskTicketId, ct, ticket.RequestEmployeeId);
                    break;
                }

                case TicketKind.D365Access:
                {
                    var approval = await _db.D365AccessApprovals.Include(a => a.Roles)
                        .FirstOrDefaultAsync(a => a.RequestId == request.RequestId, ct);
                    if (approval is null || approval.Status != D365ApprovalStatus.Completed)
                    {
                        return new TicketRetryResult(false, null,
                            "Aucune approbation D365 complétée n'a été trouvée pour cette demande — un approbateur doit d'abord remplir le formulaire (voir Administration > Approbations D365).");
                    }
                    await CreateD365AccessTicketAsync(request, approval, ct);
                    break;
                }

                default:
                    return new TicketRetryResult(false, null, $"Type de billet non pris en charge : {ticket.Kind}.");
            }
        }
        catch (Exception ex)
        {
            // The Try* methods swallow their own failures by design; this only catches something
            // thrown around them.
            _logger.LogError(ex, "Retry of {Kind} for request {RequestNumber} threw", ticket.Kind, request.RequestNumber);
            return new TicketRetryResult(false, null, ex.Message);
        }

        // Report whatever the integration actually recorded rather than assuming it worked — the
        // Try* methods catch their own exceptions and write a Failed row instead of throwing.
        var updated = await _db.RequestTickets.AsNoTracking()
            .FirstOrDefaultAsync(t => t.RequestId == request.RequestId && t.Kind == ticket.Kind && t.RequestEmployeeId == ticket.RequestEmployeeId, ct);

        if (updated is null)
        {
            // The integration decided this kind does not apply to this request (e.g. no D365 access
            // was requested), so nothing was attempted and no row was written.
            return new TicketRetryResult(false, null,
                "Aucune tentative n'a été effectuée : cette intégration ne s'applique pas à cette demande.");
        }

        return updated.Outcome == TicketOutcome.Created
            ? new TicketRetryResult(true, updated.TicketNumber, null)
            : new TicketRetryResult(false, null, updated.ErrorMessage ?? "Échec inconnu.");
    }

    private Task<bool> ChildAlreadyCreatedAsync(int requestId, TicketKind kind, CancellationToken ct) =>
        _db.RequestTickets.AsNoTracking()
            .AnyAsync(t => t.RequestId == requestId && t.Kind == kind && t.Outcome == TicketOutcome.Created, ct);

    private async Task<long?> TryCreateFreshdeskTicketAsync(Request request, AdUserInfo requester, CancellationToken ct)
    {
        string? requesterEmail = null;
        try
        {
            requesterEmail = requester.Email;
            if (string.IsNullOrWhiteSpace(requesterEmail))
            {
                throw new InvalidOperationException("Could not resolve requester email from AD.");
            }

            var ticketId = await _freshdesk.CreateTicketAsync(request, requesterEmail, ct);
            await _tickets.RecordSuccessAsync(request.RequestId, TicketKind.Freshdesk, null, ticketId.ToString(), ct);

            // Best-effort, independent of each other and of the main ticket above (which already
            // succeeded and is committed) — fanning the same submission out to two other
            // departments as Freshdesk "child" tickets of the main one.
            // Skip a child that already exists. On the submit path nothing exists yet so both run;
            // on a parent RETRY this prevents creating a second copy of a child that had already
            // succeeded, while still creating the ones that never got made.
            if (!await ChildAlreadyCreatedAsync(request.RequestId, TicketKind.FreshdeskChildWithJobCodes, ct))
            {
                await TryCreateFreshdeskChildTicketAsync(request, ticketId, requesterEmail, _freshdeskOptions.ChildGroupIdWithJobCodes, includeAllJobCodes: true, TicketKind.FreshdeskChildWithJobCodes, ct);
            }
            if (!await ChildAlreadyCreatedAsync(request.RequestId, TicketKind.FreshdeskChildWithoutJobCodes, ct))
            {
                await TryCreateFreshdeskChildTicketAsync(request, ticketId, requesterEmail, _freshdeskOptions.ChildGroupIdWithoutJobCodes, includeAllJobCodes: false, TicketKind.FreshdeskChildWithoutJobCodes, ct);
            }

            return ticketId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Freshdesk ticket for request {RequestNumber}", request.RequestNumber);
            await _tickets.RecordFailureAsync(request.RequestId, TicketKind.Freshdesk, null, ex, ct);

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
    private async Task TryCreateFreshdeskChildTicketAsync(Request request, long parentTicketId, string requesterEmail, long groupId, bool includeAllJobCodes, TicketKind kind, CancellationToken ct)
    {
        try
        {
            var childTicketId = await _freshdesk.CreateChildTicketAsync(request, parentTicketId, requesterEmail, groupId, includeAllJobCodes, ct);
            await _tickets.RecordSuccessAsync(request.RequestId, kind, null, childTicketId.ToString(), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Freshdesk child ticket (group {GroupId}) for request {RequestNumber}", groupId, request.RequestNumber);
            await _tickets.RecordFailureAsync(request.RequestId, kind, null, ex, ct);

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
    private async Task TryCreateD365BadgeTicketAsync(Request request, long? freshdeskTicketId, CancellationToken ct, int? onlyRequestEmployeeId = null)
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

        // See TryCreateTdxTicketAsync — a retry must not fan back out to every employee.
        if (onlyRequestEmployeeId is { } onlyBadgeEmployeeId)
        {
            employeesToProcess = employeesToProcess.Where(e => e.RequestEmployeeId == onlyBadgeEmployeeId).ToList();
        }

        foreach (var employee in employeesToProcess)
        {
            try
            {
                var d365JobCode = await _dynamics.CreateBadgeRequestAsync(request, employee, freshdeskTicketId, ct);
                _logger.LogInformation("Created D365 EAM badge request, jobcode {D365JobCode}, for request {RequestNumber}, employee {EmployeeName}", d365JobCode, request.RequestNumber, employee.NameSnapshot);
                await _tickets.RecordSuccessAsync(request.RequestId, TicketKind.D365Badge, employee.RequestEmployeeId, d365JobCode, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create D365 EAM badge request for request {RequestNumber}, employee {EmployeeName}", request.RequestNumber, employee.NameSnapshot);
                await _tickets.RecordFailureAsync(request.RequestId, TicketKind.D365Badge, employee.RequestEmployeeId, ex, ct);

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
    private async Task TryCreateTdxTicketAsync(Request request, AdUserInfo requester, CancellationToken ct, int? onlyRequestEmployeeId = null)
    {
        List<RequestEmployee> employeesToProcess = request.RequestType == RequestType.Offboarding
            ? request.Employees.ToList()
            : (request.Employees.FirstOrDefault(e => e.IsPrimary) ?? request.Employees.FirstOrDefault()) is { } emp
                ? [emp]
                : [];

        // A retry targets ONE row, i.e. one employee — without this the retry would re-fire the
        // integration for every employee on the request and duplicate the ones that had succeeded.
        if (onlyRequestEmployeeId is { } onlyTdxEmployeeId)
        {
            employeesToProcess = employeesToProcess.Where(e => e.RequestEmployeeId == onlyTdxEmployeeId).ToList();
        }

        var requesterInfo = requester;
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
                await _tickets.RecordSuccessAsync(request.RequestId, TicketKind.Tdx, employee.RequestEmployeeId, tdxTicketId.ToString(), ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create TDX ticket for request {RequestNumber}, employee {EmployeeName}", request.RequestNumber, employee.NameSnapshot);
                await _tickets.RecordFailureAsync(request.RequestId, TicketKind.Tdx, employee.RequestEmployeeId, ex, ct);

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

    /// <summary>Creates the D365AccessApproval "pending" row when "Accès D365" was selected —
    /// Onboarding/Réactivation only, for the primary employee, same gating pattern as the
    /// badge/alarm D365 integration. Replaces the old design (an admin pre-fills a per-job-code
    /// template before anyone needs it — in practice that table shipped and stayed completely
    /// empty). Instead, a matched D365Approver is emailed a link to a prepopulated French form; no
    /// TDX call happens here at all, only once they complete it — see CreateD365AccessTicketAsync
    /// and D365AccessApprovalsController.Complete.</summary>
    private async Task TryCreateD365AccessApprovalRequestAsync(Request request, CancellationToken ct)
    {
        if (request.RequestType == RequestType.Offboarding) return;

        var systemes = (request.AccessDetail?.Systemes.Select(s => s.Value) ?? []).ToList();
        if (!systemes.Contains(AccesD365SystemeValue)) return;

        var employee = request.Employees.FirstOrDefault(e => e.IsPrimary) ?? request.Employees.FirstOrDefault();
        if (employee is null) return;

        // Idempotent — RunAllAsync only ever runs once per real submission, but guard anyway rather
        // than risk a second Pending row (and a second round of approver emails) for one request.
        if (await _db.D365AccessApprovals.AnyAsync(a => a.RequestId == request.RequestId, ct)) return;

        var approval = new D365AccessApproval
        {
            RequestId = request.RequestId,
            RequestEmployeeId = employee.RequestEmployeeId,
            Status = D365ApprovalStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        _db.D365AccessApprovals.Add(approval);
        await _db.SaveChangesAsync(ct);

        var positionTitle = await _workday.WorkdayDemographics
            .Where(w => w.EmployeeId == employee.WorkdayEmployeeId && w.PrimaryJob == true)
            .Select(w => w.PositionTitle)
            .FirstOrDefaultAsync(ct);

        var approvers = await _d365Approvers.MatchingAsync(positionTitle, ct);
        var recipients = approvers.Where(a => !string.IsNullOrWhiteSpace(a.Email)).Select(a => a.Email!).ToList();

        var link = string.IsNullOrWhiteSpace(_appOptions.BaseUrl)
            ? $"/admin/d365-approvals/{request.RequestId}"
            : $"{_appOptions.BaseUrl.TrimEnd('/')}/admin/d365-approvals/{request.RequestId}";

        if (recipients.Count == 0)
        {
            _logger.LogWarning("No D365Approver matched request {RequestNumber} (position title {PositionTitle}) — emailing IT instead", request.RequestNumber, positionTitle);

            var fallbackSubject = $"[Cycle Emploi] Aucun approbateur D365 configuré — demande #{request.RequestNumber} — {employee.NameSnapshot}";
            var fallbackBody =
                $"La demande #{request.RequestNumber} ({request.RequestType.ToFrenchLabel()}) demande l'accès D365 pour {employee.NameSnapshot} " +
                $"(titre de poste : {positionTitle ?? "inconnu"}), mais aucun approbateur D365 (global ou pour ce titre de poste) n'est configuré " +
                "pour recevoir la demande.\n\n" +
                $"Ajoutez un approbateur dans Administration > Approbateurs D365, puis complétez le formulaire vous-même à ce lien :\n{link}\n";

            try
            {
                await _email.SendAsync(fallbackSubject, fallbackBody, ct);
            }
            catch (Exception emailEx)
            {
                _logger.LogError(emailEx, "Also failed to send the no-D365-approver-configured email for request {RequestNumber}", request.RequestNumber);
            }
            return;
        }

        var subject = $"[Cycle Emploi] Approbation D365 requise — demande #{request.RequestNumber} — {employee.NameSnapshot}";
        var body =
            $"La demande #{request.RequestNumber} ({request.RequestType.ToFrenchLabel()}) demande l'accès D365 pour {employee.NameSnapshot}.\n\n" +
            "== Détails ==\n" +
            $"Employé : {employee.NameSnapshot}\n" +
            $"Titre de poste : {positionTitle ?? "inconnu"}\n" +
            $"Département : {employee.DepartementSnapshot}\n" +
            $"Demandé par : {request.CreatedByDisplayName}\n\n" +
            $"Veuillez remplir le formulaire d'accès D365 à ce lien :\n{link}\n\n" +
            "Le formulaire est prérempli avec les informations connues de l'employé; il vous reste à préciser les rôles D365 " +
            "requis et quelques champs financiers. Un tableau des employés occupant un poste similaire, avec les rôles D365 " +
            "qu'ils détiennent déjà, y est affiché pour vous aider à décider.";

        try
        {
            await _email.SendAsync(subject, body, recipients, ct);
        }
        catch (Exception emailEx)
        {
            _logger.LogError(emailEx, "Failed to email D365 approvers for request {RequestNumber}", request.RequestNumber);
        }
    }

    /// <summary>Same recipient-matching and fallback-to-IT logic as
    /// TryCreateD365AccessApprovalRequestAsync, but the wording assumes the approval is ALREADY
    /// fully filled out (ad-hoc submissions from D365AccessRequest carry every field from the
    /// moment they're created) — the approver's job here is to review and press Envoyer, not to
    /// fill in blanks.</summary>
    public async Task NotifyD365ApproversOfAdHocRequestAsync(Request request, D365AccessApproval approval, string? positionTitle, CancellationToken ct)
    {
        var employee = request.Employees.FirstOrDefault(e => e.RequestEmployeeId == approval.RequestEmployeeId)
            ?? request.Employees.FirstOrDefault(e => e.IsPrimary) ?? request.Employees.FirstOrDefault();
        if (employee is null) return;

        var approvers = await _d365Approvers.MatchingAsync(positionTitle, ct);
        var recipients = approvers.Where(a => !string.IsNullOrWhiteSpace(a.Email)).Select(a => a.Email!).ToList();

        var link = string.IsNullOrWhiteSpace(_appOptions.BaseUrl)
            ? $"/admin/d365-approvals/{request.RequestId}"
            : $"{_appOptions.BaseUrl.TrimEnd('/')}/admin/d365-approvals/{request.RequestId}";

        if (recipients.Count == 0)
        {
            _logger.LogWarning("No D365Approver matched ad-hoc request {RequestNumber} (position title {PositionTitle}) — emailing IT instead", request.RequestNumber, positionTitle);

            var fallbackSubject = $"[Cycle Emploi] Aucun approbateur D365 configuré — demande #{request.RequestNumber} — {employee.NameSnapshot}";
            var fallbackBody =
                $"La demande #{request.RequestNumber} (accès D365, demande directe) a été remplie par {request.CreatedByDisplayName} pour " +
                $"{employee.NameSnapshot} (titre de poste : {positionTitle ?? "inconnu"}), mais aucun approbateur D365 (global ou pour ce titre " +
                "de poste) n'est configuré pour recevoir la demande.\n\n" +
                $"Ajoutez un approbateur dans Administration > Approbateurs D365, puis envoyez le formulaire vous-même à ce lien :\n{link}\n";

            try
            {
                await _email.SendAsync(fallbackSubject, fallbackBody, ct);
            }
            catch (Exception emailEx)
            {
                _logger.LogError(emailEx, "Also failed to send the no-D365-approver-configured email for ad-hoc request {RequestNumber}", request.RequestNumber);
            }
            return;
        }

        var subject = $"[Cycle Emploi] Approbation D365 requise — demande #{request.RequestNumber} — {employee.NameSnapshot}";
        var body =
            $"{request.CreatedByDisplayName} a rempli une demande d'accès D365 pour {employee.NameSnapshot} et l'a soumise pour révision.\n\n" +
            "== Détails ==\n" +
            $"Employé : {employee.NameSnapshot}\n" +
            $"Titre de poste : {positionTitle ?? "inconnu"}\n" +
            $"Département : {employee.DepartementSnapshot}\n" +
            $"Rempli par : {request.CreatedByDisplayName}\n\n" +
            $"Le formulaire est déjà entièrement rempli — vérifiez-le et appuyez sur « Envoyer » pour créer le billet TDX :\n{link}\n";

        try
        {
            await _email.SendAsync(subject, body, recipients, ct);
        }
        catch (Exception emailEx)
        {
            _logger.LogError(emailEx, "Failed to email D365 approvers for ad-hoc request {RequestNumber}", request.RequestNumber);
        }
    }

    /// <summary>Builds and sends the real TDX "D365 - Access" ticket from a COMPLETED approval's
    /// saved fields. Called both right after an approver submits the form
    /// (D365AccessApprovalsController.Complete) and by RetryAsync if that TDX call itself failed —
    /// the approver's decisions (which roles, what approval limit, ...) are never re-asked for on a
    /// retry, only the downstream TDX call is re-attempted, same as every other ticket kind.</summary>
    public async Task<TicketRetryResult> CreateD365AccessTicketAsync(Request request, D365AccessApproval approval, CancellationToken ct)
    {
        var employee = request.Employees.FirstOrDefault(e => e.RequestEmployeeId == approval.RequestEmployeeId)
            ?? request.Employees.FirstOrDefault(e => e.IsPrimary) ?? request.Employees.FirstOrDefault();
        if (employee is null)
        {
            return new TicketRetryResult(false, null, "Aucun employé sur cette demande.");
        }

        try
        {
            var requesterName = request.CreatedByDisplayName;
            var requesterEmail = request.RequesterEmail;
            if (string.IsNullOrWhiteSpace(requesterEmail))
            {
                throw new InvalidOperationException("Cette demande n'a pas de courriel de demandeur enregistré (elle a été soumise avant que ce champ existe).");
            }

            var workdayInfo = await _workday.WorkdayDemographics
                .Where(w => w.EmployeeId == employee.WorkdayEmployeeId && w.PrimaryJob == true)
                .Select(w => new { w.WorkEmail, w.Email, w.ManagerId, w.Manager })
                .FirstOrDefaultAsync(ct);
            if (workdayInfo is null)
            {
                throw new InvalidOperationException($"No active Workday record found for employee {employee.WorkdayEmployeeId}.");
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
                    .Where(w => w.EmployeeId == workdayInfo.ManagerId && w.PrimaryJob == true)
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
                JobTitle: approval.JobTitleEnglish,
                LegalEntity: approval.LegalEntity ?? "",
                DepartmentNumber: approval.DepartmentNumber ?? "",
                LevyEmployee: approval.LevyEmployee ?? false,
                ManagerName: managerName,
                StartDate: request.OnboardingDetail?.DateEntreePrevue,
                Roles: approval.Roles.Select(r => r.Role).ToList(),
                ApprovalLimit: approval.ApprovalLimit ?? 0,
                ApAccessDetails: approval.ApAccessDetails,
                AdditionalLegalEntities: approval.AdditionalLegalEntities,
                DefaultShippingAddress: approval.DefaultShippingAddress,
                Comments: approval.Comments,
                AccessType: approval.AccessType ?? "New Access");

            var ticketId = await _tdx.CreateD365AccessTicketAsync(input, ct);
            _logger.LogInformation("Created D365 Access TDX ticket {TicketId} for request {RequestNumber}, employee {EmployeeName}", ticketId, request.RequestNumber, employee.NameSnapshot);
            await _tickets.RecordSuccessAsync(request.RequestId, TicketKind.D365Access, employee.RequestEmployeeId, ticketId.ToString(), ct);
            return new TicketRetryResult(true, ticketId.ToString(), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create D365 Access TDX ticket for request {RequestNumber}, employee {EmployeeName}", request.RequestNumber, employee.NameSnapshot);
            await _tickets.RecordFailureAsync(request.RequestId, TicketKind.D365Access, employee.RequestEmployeeId, ex, ct);

            var subject = $"[Cycle Emploi] Échec de création de billet TDX D365 Access — demande #{request.RequestNumber} — {employee.NameSnapshot}";
            var body =
                $"Un approbateur D365 a complété le formulaire pour la demande #{request.RequestNumber} ({request.RequestType.ToFrenchLabel()}), " +
                $"mais la création du billet TDX « D365 - Access » pour {employee.NameSnapshot} a échoué. " +
                "Le billet peut être relancé depuis Administration > Demandes (bouton Réessayer) une fois la cause corrigée — " +
                "les informations déjà saisies par l'approbateur sont conservées et ne seront pas redemandées.\n\n" +
                "== Détails de l'erreur ==\n" +
                $"Type: {ex.GetType().Name}\n" +
                $"Message: {ex.Message}\n" +
                (ex.InnerException is not null ? $"Cause interne: {ex.InnerException.Message}\n" : "") +
                $"Survenue: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC\n";

            try
            {
                await _email.SendAsync(subject, body, ct);
            }
            catch (Exception emailEx)
            {
                _logger.LogError(emailEx, "Also failed to send the D365-Access-ticket-failure notification email for request {RequestNumber}, employee {EmployeeName}", request.RequestNumber, employee.NameSnapshot);
            }

            return new TicketRetryResult(false, null, ex.Message);
        }
    }
}
