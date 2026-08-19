using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TremblantLifecycle.Api.Data;
using TremblantLifecycle.Api.Models.Entities;

namespace TremblantLifecycle.Api.Services;

/// <summary>Creates a Freshdesk ticket for every submitted request, in the "RH - Général" queue
/// (group_id/email_config_id/type confirmed against the real Freshdesk instance — see
/// tremblantsmt.freshdesk.com's /api/v2/groups, /api/v2/email_configs, /api/v2/ticket_fields).
/// Subject/description come from admin-editable TicketTemplates (see TicketTemplateDefaults) rather
/// than hardcoded strings, so IT/HR can change what information a ticket contains without a code
/// change.</summary>
public class FreshdeskService : IFreshdeskService
{
    private readonly HttpClient _http;
    private readonly FreshdeskOptions _options;
    private readonly WorkdayContext _workday;
    private readonly ITicketTemplateService _templates;

    public FreshdeskService(HttpClient http, IOptions<FreshdeskOptions> options, WorkdayContext workday, ITicketTemplateService templates)
    {
        _http = http;
        _options = options.Value;
        _workday = workday;
        _templates = templates;
    }

    public async Task<long> CreateTicketAsync(Request request, string requesterEmail, CancellationToken ct)
    {
        var subject = await BuildSubjectAsync(request, ct);
        var description = await BuildMainContentAsync(request, ct);

        var payload = new
        {
            description,
            subject,
            email = requesterEmail,
            type = _options.TicketType,
            email_config_id = _options.EmailConfigId,
            group_id = _options.GroupId,
            priority = 1,
            status = 2,
            tags = Array.Empty<string>()
        };

        return await PostTicketAsync(payload, ct);
    }

    public async Task<long> CreateChildTicketAsync(Request request, long parentTicketId, string requesterEmail, long groupId, bool includeAllJobCodes, CancellationToken ct)
    {
        var subject = await BuildSubjectAsync(request, ct);
        var description = await BuildChildContentAsync(request, includeAllJobCodes, ct);

        var payload = new
        {
            description,
            subject,
            email = requesterEmail,
            type = _options.TicketType,
            email_config_id = _options.EmailConfigId,
            group_id = groupId,
            priority = 1,
            status = 2,
            parent_id = parentTicketId,
            tags = Array.Empty<string>()
        };

        return await PostTicketAsync(payload, ct);
    }

    private async Task<string> BuildSubjectAsync(Request request, CancellationToken ct)
    {
        var employeeNames = string.Join(", ", request.Employees.Select(e => e.NameSnapshot));
        var template = await _templates.GetContentAsync(TicketTemplateKeys.FreshdeskSubject, ct);
        return TicketTemplateRenderer.Render(template, new Dictionary<string, string?>
        {
            ["RequestTypeLabel"] = request.RequestType.ToFrenchLabel(),
            ["EmployeeNames"] = employeeNames,
            ["RequestNumber"] = request.RequestNumber
        });
    }

    private async Task<long> PostTicketAsync(object payload, CancellationToken ct)
    {
        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"https://{_options.Subdomain}/api/v2/tickets")
        {
            Content = content
        };
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_options.ApiKey}:X")));

        using var response = await _http.SendAsync(requestMessage, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new FreshdeskTicketException($"Freshdesk returned {(int)response.StatusCode} {response.ReasonPhrase}: {responseBody}");
        }

        using var doc = JsonDocument.Parse(responseBody);
        return doc.RootElement.GetProperty("id").GetInt64();
    }

    /// <summary>Builds the per-employee HTML block shared by both Freshdesk child templates — see
    /// TicketTemplateDefaults' doc comment for why this is a pre-rendered block rather than a set of
    /// flat placeholders (the per-employee Workday lookups don't reduce to a single value).</summary>
    private async Task<string> BuildEmployeesDetailBlocAsync(Request request, bool includeAllJobCodes, CancellationToken ct)
    {
        var isOffboarding = request.RequestType == RequestType.Offboarding;
        var dateLabel = isOffboarding ? "Date de fin (dernière journée)" : "Date de début prévue";
        var dateValue = isOffboarding
            ? request.OffboardingDetail?.DerniereJournee
            : request.OnboardingDetail?.DateEntreePrevue;

        var sb = new StringBuilder();
        foreach (var emp in request.Employees)
        {
            // Pay_Group is a distinct Workday field from this app's own "Règle de paye" wizard
            // input — looked up live rather than snapshotted, from the employee's primary job
            // assignment (matches how EmployeesController resolves canonical employee data).
            var payGroup = await _workday.WorkdayDemographics
                .Where(w => w.EmployeeId == emp.WorkdayEmployeeId && w.PrimaryJob == 1)
                .Select(w => w.PayGroup)
                .FirstOrDefaultAsync(ct);

            sb.Append("<p>");
            sb.Append($"<b>Nom employé:</b> {emp.NameSnapshot}<br>");
            sb.Append($"<b>{dateLabel}:</b> {(dateValue is { } date ? date.ToString("yyyy-MM-dd") : "—")}<br>");
            sb.Append($"<b>Gestionnaire:</b> {emp.GestionnaireSnapshot}<br>");
            sb.Append($"<b>Titre du poste:</b> {emp.PositionSnapshot}<br>");
            sb.Append($"<b>Groupe de paye:</b> {payGroup ?? "—"}");

            if (includeAllJobCodes)
            {
                var jobCodes = await _workday.WorkdayDemographics
                    .Where(w => w.EmployeeId == emp.WorkdayEmployeeId && w.JobCode != null && w.JobCode != "")
                    .Select(w => w.JobCode!)
                    .Distinct()
                    .ToListAsync(ct);
                sb.Append($"<br><b>Tous les codes d'emploi:</b> {(jobCodes.Count > 0 ? string.Join(", ", jobCodes) : "—")}");
            }

            sb.Append("</p>");
        }

        return sb.ToString();
    }

    private async Task<string> BuildChildContentAsync(Request request, bool includeAllJobCodes, CancellationToken ct)
    {
        var key = includeAllJobCodes ? TicketTemplateKeys.FreshdeskChildWithJobCodes : TicketTemplateKeys.FreshdeskChildWithoutJobCodes;
        var template = await _templates.GetContentAsync(key, ct);
        var bloc = await BuildEmployeesDetailBlocAsync(request, includeAllJobCodes, ct);

        return TicketTemplateRenderer.Render(template, new Dictionary<string, string?>
        {
            ["RequestNumber"] = request.RequestNumber,
            ["RequestTypeLabel"] = request.RequestType.ToFrenchLabel(),
            ["EmployeesDetailBloc"] = bloc
        });
    }

    private async Task<string> BuildMainContentAsync(Request request, CancellationToken ct)
    {
        if (request.RequestType == RequestType.Offboarding)
        {
            var template = await _templates.GetContentAsync(TicketTemplateKeys.FreshdeskMainOffboarding, ct);
            var employeesListe = string.Concat(request.Employees.Select(emp =>
                $"<li>{emp.NameSnapshot} (#{emp.WorkdayEmployeeId}) — {emp.PositionSnapshot} — {emp.DepartementSnapshot}</li>"));

            var d = request.OffboardingDetail;
            return TicketTemplateRenderer.Render(template, new Dictionary<string, string?>
            {
                ["RequestNumber"] = request.RequestNumber,
                ["RequestTypeLabel"] = request.RequestType.ToFrenchLabel(),
                ["RequestedBy"] = request.CreatedByDisplayName,
                ["CreatedDate"] = request.CreatedAt.ToString("yyyy-MM-dd"),
                ["EmployeesListe"] = employeesListe,
                ["DerniereJournee"] = d?.DerniereJournee is { } date ? date.ToString("yyyy-MM-dd") : null,
                ["IndemniteVacances"] = d?.IndemniteVacances,
                ["RaisonArret"] = d?.RaisonArret,
                ["DetailsRaison"] = d?.DetailsRaison,
                ["Reembaucheriez"] = d?.Reembaucheriez,
                // Deliberately the ONLY comment field included here — commentairesIT/Stationnement/
                // Redingote are excluded on purpose, for other systems/tickets, not this one (per
                // explicit product decision). CommentaireRH lives in a physically separate,
                // access-restricted table (see OffboardingConfidentialComment's doc comment) —
                // including it here is a deliberate exception for this specific ticket/group ("RH -
                // Général"), not a general precedent.
                ["CommentaireRH"] = request.ConfidentialComment?.CommentaireRH
            });
        }
        else
        {
            var template = await _templates.GetContentAsync(TicketTemplateKeys.FreshdeskMainOnboarding, ct);
            var emp = request.Employees.FirstOrDefault();
            var d = request.OnboardingDetail;
            var a = request.AccessDetail;
            var eq = request.EquipmentDetail;
            var app = request.ApplicationsDetail;

            return TicketTemplateRenderer.Render(template, new Dictionary<string, string?>
            {
                ["RequestNumber"] = request.RequestNumber,
                ["RequestTypeLabel"] = request.RequestType.ToFrenchLabel(),
                ["RequestedBy"] = request.CreatedByDisplayName,
                ["CreatedDate"] = request.CreatedAt.ToString("yyyy-MM-dd"),
                ["EmployeeName"] = emp?.NameSnapshot,
                ["EmployeeId"] = emp?.WorkdayEmployeeId,
                ["Poste"] = emp?.PositionSnapshot,
                ["Departement"] = emp?.DepartementSnapshot,
                ["Gestionnaire"] = emp?.GestionnaireSnapshot,
                ["TypeEmploi"] = emp?.TypeEmploiSnapshot,
                ["DateEntreePrevue"] = d?.DateEntreePrevue is { } date ? date.ToString("yyyy-MM-dd") : null,
                ["RegleDePaye"] = d?.RegleDePaye,
                ["RegleDePayeCommentaire"] = d?.RegleDePayeCommentaire,
                ["SystemesAcces"] = JoinOrNull(a?.Systemes.Select(s => s.Value)),
                ["ZonesBadge"] = a?.BadgeZones,
                ["PosHebergement"] = JoinOrNull(a?.PosHebergement.Select(p => p.Value)),
                ["Stationnement"] = a?.Stationnement,
                ["JustificationAcces"] = a?.Justification,
                ["Equipements"] = JoinOrNull(eq?.Equipements.Select(x => x.Value)),
                ["NotesEquipement"] = eq?.Notes,
                ["Applications"] = JoinOrNull(app?.Applications.Select(x => x.Value)),
                ["AutreLogiciel"] = app?.AutreLogiciel
            });
        }
    }

    private static string? JoinOrNull(IEnumerable<string>? values)
    {
        var list = values?.ToList();
        return list is null || list.Count == 0 ? null : string.Join(", ", list);
    }
}
