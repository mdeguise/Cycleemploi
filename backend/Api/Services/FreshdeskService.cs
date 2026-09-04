using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TremblantLifecycle.Api.Models.Entities;

namespace TremblantLifecycle.Api.Services;

/// <summary>Creates a Freshdesk ticket for every submitted request, in the "RH - Général" queue
/// (group_id/email_config_id/type confirmed against the real Freshdesk instance — see
/// tremblantsmt.freshdesk.com's /api/v2/groups, /api/v2/email_configs, /api/v2/ticket_fields).
/// Subject/body come from admin-editable TicketTemplates (see TicketTemplateDefaults), fully split
/// by request type, rather than hardcoded strings, so IT/HR can change what information a ticket
/// contains — and add any of the fields in TicketTemplateFieldCatalog — without a code change.</summary>
public class FreshdeskService : IFreshdeskService
{
    private readonly HttpClient _http;
    private readonly FreshdeskOptions _options;
    private readonly ITicketTemplateService _templates;
    private readonly IEmployeeFieldsService _employeeFields;

    public FreshdeskService(HttpClient http, IOptions<FreshdeskOptions> options, ITicketTemplateService templates, IEmployeeFieldsService employeeFields)
    {
        _http = http;
        _options = options.Value;
        _templates = templates;
        _employeeFields = employeeFields;
    }

    public async Task<long> CreateTicketAsync(Request request, string requesterEmail, CancellationToken ct)
    {
        var isOffboarding = request.RequestType == RequestType.Offboarding;
        var subject = await BuildSubjectAsync(request, isOffboarding, ct);
        var description = await BuildMainContentAsync(request, isOffboarding, ct);

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

    public Task<long> CreateHorairesTicketAsync(Request request, string requesterEmail, CancellationToken ct) =>
        CreateFannedOutTicketAsync(request, requesterEmail, _options.HorairesGroupId, isOffboarding =>
            BuildHorairesContentAsync(request, isOffboarding, ct), ct);

    public Task<long> CreateRedingoteTicketAsync(Request request, string requesterEmail, CancellationToken ct) =>
        CreateFannedOutTicketAsync(request, requesterEmail, _options.RedingoteGroupId, isOffboarding =>
            BuildRedingoteContentAsync(request, isOffboarding, ct), ct);

    public Task<long> CreateStationnementTicketAsync(Request request, string requesterEmail, CancellationToken ct) =>
        CreateFannedOutTicketAsync(request, requesterEmail, _options.StationnementGroupId, isOffboarding =>
            BuildStationnementContentAsync(request, isOffboarding, ct), ct);

    /// <summary>Shared by every independent (non-main) Freshdesk ticket — same subject as the main
    /// ticket, own group, own content, and deliberately NO parent_id: these are standalone tickets,
    /// not Freshdesk's Parent-child ticketing feature, so a failure/edit on one never touches the
    /// others. Correlated only by sharing the same subject text and request number.</summary>
    private async Task<long> CreateFannedOutTicketAsync(Request request, string requesterEmail, long groupId, Func<bool, Task<string>> buildDescription, CancellationToken ct)
    {
        var isOffboarding = request.RequestType == RequestType.Offboarding;
        var subject = await BuildSubjectAsync(request, isOffboarding, ct);
        var description = await buildDescription(isOffboarding);

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
            tags = Array.Empty<string>()
        };

        return await PostTicketAsync(payload, ct);
    }

    private async Task<string> BuildSubjectAsync(Request request, bool isOffboarding, CancellationToken ct)
    {
        var key = isOffboarding ? TicketTemplateKeys.FreshdeskSubjectOffboarding : TicketTemplateKeys.FreshdeskSubjectOnboarding;
        var template = await _templates.GetContentAsync(key, ct);
        var employeeNames = string.Join(", ", request.Employees.Select(e => e.NameSnapshot));

        return TicketTemplateRenderer.RenderInline(template, new Dictionary<string, string?>
        {
            ["RequestTypeLabel"] = request.RequestType.ToFrenchLabel(),
            ["EmployeeNames"] = employeeNames,
            ["RequestNumber"] = request.RequestNumber
        });
    }

    private async Task<string> BuildMainContentAsync(Request request, bool isOffboarding, CancellationToken ct)
    {
        var employeeValuesList = new List<IReadOnlyDictionary<string, string?>>();
        foreach (var emp in request.Employees)
        {
            employeeValuesList.Add(await _employeeFields.ResolveAsync(emp, ct));
        }

        if (isOffboarding)
        {
            var template = await _templates.GetContentAsync(TicketTemplateKeys.FreshdeskMainOffboarding, ct);
            var d = request.OffboardingDetail;
            var requestValues = new Dictionary<string, string?>
            {
                ["RequestNumber"] = request.RequestNumber,
                ["RequestTypeLabel"] = request.RequestType.ToFrenchLabel(),
                ["RequestedBy"] = request.CreatedByDisplayName,
                ["CreatedDate"] = request.CreatedAt.ToString("yyyy-MM-dd"),
                ["DerniereJournee"] = d?.DerniereJournee is { } date ? date.ToString("yyyy-MM-dd") : null,
                ["IndemniteVacances"] = d?.IndemniteVacances,
                ["RaisonArret"] = d?.RaisonArret,
                ["DetailsRaison"] = d?.DetailsRaison,
                ["Reembaucheriez"] = d?.Reembaucheriez,
                ["MotifNonAdmissibilite"] = d?.MotifNonAdmissibilite,
                ["DateRetourConnue"] = d?.DateRetourConnue,
                ["DateRetourTravail"] = d?.DateRetourTravail is { } retourDate ? retourDate.ToString("yyyy-MM-dd") : null,
                ["PreavisRecu"] = d?.PreavisRecu,
                ["CommentairesIT"] = d?.CommentairesIT,
                ["CommentairesStationnement"] = d?.CommentairesStationnement,
                ["CommentairesPuceAcces"] = d?.CommentairesPuceAcces,
                ["CommentairesRedingote"] = d?.CommentairesRedingote,
                // CommentaireRH lives in a physically separate, access-restricted table (see
                // OffboardingConfidentialComment's doc comment) — including it here is a deliberate
                // exception for this specific ticket ("RH - Général"), not a general precedent.
                ["CommentaireRH"] = request.ConfidentialComment?.CommentaireRH
            };
            return TicketTemplateRenderer.RenderBlock(template, requestValues, employeeValuesList);
        }
        else
        {
            var template = await _templates.GetContentAsync(TicketTemplateKeys.FreshdeskMainOnboarding, ct);
            var d = request.OnboardingDetail;
            var a = request.AccessDetail;
            var eq = request.EquipmentDetail;
            var app = request.ApplicationsDetail;
            var requestValues = new Dictionary<string, string?>
            {
                ["RequestNumber"] = request.RequestNumber,
                ["RequestTypeLabel"] = request.RequestType.ToFrenchLabel(),
                ["RequestedBy"] = request.CreatedByDisplayName,
                ["CreatedDate"] = request.CreatedAt.ToString("yyyy-MM-dd"),
                ["DateEntreePrevue"] = d?.DateEntreePrevue is { } date ? date.ToString("yyyy-MM-dd") : null,
                ["RegleDePaye"] = d?.RegleDePaye,
                ["RegleDePayeCommentaire"] = d?.RegleDePayeCommentaire,
                ["SystemesAcces"] = JoinOrNull(a?.Systemes.Select(s => s.Value)),
                ["ZonesBadge"] = a?.BadgeZones,
                ["PosHebergement"] = JoinOrNull(a?.PosHebergement.Select(p => p.Value)),
                ["Stationnement"] = a?.Stationnement,
                ["JustificationAcces"] = a?.Justification,
                ["CodeAlarmeDetails"] = a?.CodeAlarmeDetails,
                ["Equipements"] = JoinOrNull(eq?.Equipements.Select(x => x.Value)),
                ["NotesEquipement"] = eq?.Notes,
                ["Applications"] = JoinOrNull(app?.Applications.Select(x => x.Value)),
                ["AutreLogiciel"] = app?.AutreLogiciel,
                ["CommentairesIT"] = d?.CommentairesIT,
                ["CommentairesStationnement"] = d?.CommentairesStationnement,
                ["CommentairesPuceAcces"] = d?.CommentairesPuceAcces,
                ["CommentairesRedingote"] = d?.CommentairesRedingote,
                // Same deliberate exception as FreshdeskMainOffboarding's CommentaireRH — see that
                // branch's comment. Lives in OnboardingConfidentialComment, a physically separate,
                // access-restricted table (see its doc comment).
                ["CommentaireRH"] = request.OnboardingConfidentialComment?.CommentaireRH
            };
            return TicketTemplateRenderer.RenderBlock(template, requestValues, employeeValuesList);
        }
    }

    /// <summary>"RH - Horaires" (FreshdeskOptions.HorairesGroupId) — the payroll/scheduling
    /// department's ticket, including the employee's full job-code history.</summary>
    private async Task<string> BuildHorairesContentAsync(Request request, bool isOffboarding, CancellationToken ct)
    {
        var key = isOffboarding ? TicketTemplateKeys.FreshdeskChildWithCodesOffboarding : TicketTemplateKeys.FreshdeskChildWithCodesOnboarding;
        return await RenderFannedOutBlockAsync(request, isOffboarding, key, ct);
    }

    /// <summary>"RH - Redingote" (FreshdeskOptions.RedingoteGroupId) — the uniforms/équipement
    /// department's ticket. No job-code history (that's Horaires' distinguishing field); includes
    /// CommentairesRedingote instead.</summary>
    private async Task<string> BuildRedingoteContentAsync(Request request, bool isOffboarding, CancellationToken ct)
    {
        var key = isOffboarding ? TicketTemplateKeys.FreshdeskChildWithoutCodesOffboarding : TicketTemplateKeys.FreshdeskChildWithoutCodesOnboarding;
        return await RenderFannedOutBlockAsync(request, isOffboarding, key, ct);
    }

    /// <summary>"SAC - ISAC" (FreshdeskOptions.StationnementGroupId) — the parking department's
    /// ticket, with the request's Stationnement selection and comment.</summary>
    private async Task<string> BuildStationnementContentAsync(Request request, bool isOffboarding, CancellationToken ct)
    {
        var key = isOffboarding ? TicketTemplateKeys.FreshdeskStationnementOffboarding : TicketTemplateKeys.FreshdeskStationnementOnboarding;
        return await RenderFannedOutBlockAsync(request, isOffboarding, key, ct);
    }

    /// <summary>Shared request-level field set for every fanned-out (non-main) ticket — covers
    /// every field any of their templates can reference (see TicketTemplateDefaults'
    /// OnboardingChildRequestFields/OffboardingChildRequestFields/*StationnementRequestFields).
    /// Harmless to compute fields a given template doesn't use: TicketTemplateRenderer only reads
    /// the keys its own saved content actually references.</summary>
    private async Task<string> RenderFannedOutBlockAsync(Request request, bool isOffboarding, string templateKey, CancellationToken ct)
    {
        var template = await _templates.GetContentAsync(templateKey, ct);

        var requestValues = new Dictionary<string, string?>
        {
            ["RequestTypeLabel"] = request.RequestType.ToFrenchLabel(),
            ["DateEntreePrevue"] = !isOffboarding && request.OnboardingDetail?.DateEntreePrevue is { } entreeDate ? entreeDate.ToString("yyyy-MM-dd") : null,
            ["DerniereJournee"] = isOffboarding && request.OffboardingDetail?.DerniereJournee is { } derniereDate ? derniereDate.ToString("yyyy-MM-dd") : null,
            ["CommentairesRedingote"] = isOffboarding ? request.OffboardingDetail?.CommentairesRedingote : request.OnboardingDetail?.CommentairesRedingote,
            ["Stationnement"] = request.AccessDetail?.Stationnement,
            ["CommentairesStationnement"] = isOffboarding ? request.OffboardingDetail?.CommentairesStationnement : request.OnboardingDetail?.CommentairesStationnement
        };

        var employeeValuesList = new List<IReadOnlyDictionary<string, string?>>();
        foreach (var emp in request.Employees)
        {
            employeeValuesList.Add(await _employeeFields.ResolveAsync(emp, ct));
        }

        return TicketTemplateRenderer.RenderBlock(template, requestValues, employeeValuesList);
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

    /// <summary>Freshdesk status codes, confirmed against the live instance's /api/v2/ticket_fields:
    /// 2 Open, 3 Pending, 4 Resolved, 5 Closed. Resolved counts as closed for this screen — the work
    /// is done and nobody is waiting on it.</summary>
    public async Task<LiveTicketStatus> GetTicketStatusAsync(long ticketId, CancellationToken ct)
    {
        try
        {
            using var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"https://{_options.Subdomain}/api/v2/tickets/{ticketId}");
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue(
                "Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_options.ApiKey}:X")));

            using var response = await _http.SendAsync(requestMessage, ct);
            if (!response.IsSuccessStatusCode) return LiveTicketStatus.Unknown;

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("status", out var statusEl)) return LiveTicketStatus.Unknown;

            return statusEl.GetInt32() switch
            {
                2 => new LiveTicketStatus(LiveTicketState.Open, "Ouvert"),
                3 => new LiveTicketStatus(LiveTicketState.Open, "En attente"),
                4 => new LiveTicketStatus(LiveTicketState.Closed, "Résolu"),
                5 => new LiveTicketStatus(LiveTicketState.Closed, "Fermé"),
                _ => LiveTicketStatus.Unknown
            };
        }
        catch
        {
            // Never throws by contract — see IFreshdeskService.
            return LiveTicketStatus.Unknown;
        }
    }

    private static string? JoinOrNull(IEnumerable<string>? values)
    {
        var list = values?.ToList();
        return list is null || list.Count == 0 ? null : string.Join(", ", list);
    }
}
