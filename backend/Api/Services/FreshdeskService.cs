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

    public async Task<long> CreateChildTicketAsync(Request request, long parentTicketId, string requesterEmail, long groupId, bool includeAllJobCodes, CancellationToken ct)
    {
        var isOffboarding = request.RequestType == RequestType.Offboarding;
        var subject = await BuildSubjectAsync(request, isOffboarding, ct);
        var description = await BuildChildContentAsync(request, isOffboarding, includeAllJobCodes, ct);

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
                // Deliberately the ONLY comment field available here — commentairesIT/
                // Stationnement/Redingote are for other systems/tickets, not this one (per explicit
                // product decision). CommentaireRH lives in a physically separate, access-restricted
                // table (see OffboardingConfidentialComment's doc comment) — including it here is a
                // deliberate exception for this specific ticket ("RH - Général"), not a general
                // precedent.
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
                ["Equipements"] = JoinOrNull(eq?.Equipements.Select(x => x.Value)),
                ["NotesEquipement"] = eq?.Notes,
                ["Applications"] = JoinOrNull(app?.Applications.Select(x => x.Value)),
                ["AutreLogiciel"] = app?.AutreLogiciel
            };
            return TicketTemplateRenderer.RenderBlock(template, requestValues, employeeValuesList);
        }
    }

    private async Task<string> BuildChildContentAsync(Request request, bool isOffboarding, bool includeAllJobCodes, CancellationToken ct)
    {
        string key;
        if (isOffboarding)
        {
            key = includeAllJobCodes ? TicketTemplateKeys.FreshdeskChildWithCodesOffboarding : TicketTemplateKeys.FreshdeskChildWithoutCodesOffboarding;
        }
        else
        {
            key = includeAllJobCodes ? TicketTemplateKeys.FreshdeskChildWithCodesOnboarding : TicketTemplateKeys.FreshdeskChildWithoutCodesOnboarding;
        }
        var template = await _templates.GetContentAsync(key, ct);

        var requestValues = new Dictionary<string, string?>
        {
            ["RequestTypeLabel"] = request.RequestType.ToFrenchLabel(),
            ["DateEntreePrevue"] = !isOffboarding && request.OnboardingDetail?.DateEntreePrevue is { } entreeDate ? entreeDate.ToString("yyyy-MM-dd") : null,
            ["DerniereJournee"] = isOffboarding && request.OffboardingDetail?.DerniereJournee is { } derniereDate ? derniereDate.ToString("yyyy-MM-dd") : null
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

    private static string? JoinOrNull(IEnumerable<string>? values)
    {
        var list = values?.ToList();
        return list is null || list.Count == 0 ? null : string.Join(", ", list);
    }
}
