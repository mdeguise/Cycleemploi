using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using TremblantLifecycle.Api.Models.Entities;

namespace TremblantLifecycle.Api.Services;

public class PowerAutomateEamService : IDynamicsEamService
{
    private readonly HttpClient _http;
    private readonly PowerAutomateOptions _options;

    public PowerAutomateEamService(HttpClient http, IOptions<PowerAutomateOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<string> CreateBadgeRequestAsync(Request request, RequestEmployee employee, long? freshdeskTicketId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.BadgeRequestWebhookUrl))
        {
            throw new DynamicsEamException("Power Automate badge-request webhook URL not configured.");
        }

        var access = request.AccessDetail;
        var systemes = access?.Systemes.Select(s => s.Value).ToList() ?? [];

        // debutantLe (start date) and derniereJournee (last day) are two distinct fields — a
        // termination has no "date d'entrée", and reusing debutantLe for both was confusing, so
        // each request type only ever populates the one that actually applies to it.
        var debutantLe = request.RequestType == RequestType.Offboarding
            ? null
            : request.OnboardingDetail?.DateEntreePrevue?.ToString("yyyy-MM-dd");
        var derniereJournee = request.RequestType == RequestType.Offboarding
            ? request.OffboardingDetail?.DerniereJournee?.ToString("yyyy-MM-dd")
            : null;

        // Stationnement and puce d'accès are always kept as two separate fields — different
        // departments manage each, even though they used to share one free-text box on the
        // termination form (now split into two — see Step3DepartmentComments.tsx). Onboarding/
        // Réactivation only has a dedicated Stationnement field; "puce d'accès" concerns there are
        // already fully captured via BesoinBadgeAcces/ZonesOuEdifices above, so PuceAcces is null
        // for those two types rather than duplicating that data under a different name.
        string? stationnement;
        string? puceAcces;
        if (request.RequestType == RequestType.Offboarding)
        {
            stationnement = request.OffboardingDetail?.CommentairesStationnement;
            puceAcces = request.OffboardingDetail?.CommentairesPuceAcces;
        }
        else
        {
            stationnement = access?.Stationnement;
            puceAcces = null;
        }

        var payload = new BadgeRequestPayload
        {
            RequestNumber = request.RequestNumber,
            TypeDemande = request.RequestType.ToFrenchLabel(),
            NomEmploye = employee.NameSnapshot,
            PositionTitle = employee.PositionSnapshot,
            JobCode = employee.CodeEmploiSnapshot,
            Departement = employee.DepartementSnapshot,
            Statut = employee.TypeEmploiSnapshot,
            DebutantLe = debutantLe,
            DerniereJournee = derniereJournee,
            Manager = employee.GestionnaireSnapshot,
            DemandePar = request.CreatedByDisplayName,
            BesoinBadgeAcces = systemes.Contains("Badge d'accès aux édifices"),
            ZonesOuEdifices = access?.BadgeZones,
            BesoinCodeAlarme = systemes.Contains("Besoin de code d'alarme"),
            DetailsCodeAlarme = access?.CodeAlarmeDetails,
            Stationnement = stationnement,
            PuceAcces = puceAcces,
            FreshdeskTicketId = freshdeskTicketId
        };

        using var response = await _http.PostAsJsonAsync(_options.BadgeRequestWebhookUrl, payload, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new DynamicsEamException($"Power Automate webhook returned {(int)response.StatusCode} {response.ReasonPhrase}: {errorBody}");
        }

        // The flow may run async (default "When an HTTP request is received" behavior returns 202
        // with no body) or synchronously return the created D365 RequestId via a Response action —
        // support both rather than requiring the flow author to add one.
        try
        {
            var result = await response.Content.ReadFromJsonAsync<BadgeRequestResponse>(cancellationToken: ct);
            if (!string.IsNullOrWhiteSpace(result?.RequestId))
            {
                return result.RequestId;
            }
        }
        catch
        {
            // No JSON body (e.g. a plain 202 Accepted) — not an error, just nothing to report back.
        }

        return $"(soumis via webhook, ID D365 non retourné — statut {(int)response.StatusCode})";
    }

    private class BadgeRequestPayload
    {
        [JsonPropertyName("requestNumber")]
        public string RequestNumber { get; set; } = "";

        [JsonPropertyName("typeDemande")]
        public string TypeDemande { get; set; } = "";

        [JsonPropertyName("nomEmploye")]
        public string? NomEmploye { get; set; }

        [JsonPropertyName("positionTitle")]
        public string? PositionTitle { get; set; }

        [JsonPropertyName("jobcode")]
        public string? JobCode { get; set; }

        [JsonPropertyName("departement")]
        public string? Departement { get; set; }

        [JsonPropertyName("statut")]
        public string? Statut { get; set; }

        [JsonPropertyName("debutantLe")]
        public string? DebutantLe { get; set; }

        [JsonPropertyName("derniereJournee")]
        public string? DerniereJournee { get; set; }

        [JsonPropertyName("manager")]
        public string? Manager { get; set; }

        [JsonPropertyName("demandePar")]
        public string? DemandePar { get; set; }

        [JsonPropertyName("besoinBadgeAcces")]
        public bool BesoinBadgeAcces { get; set; }

        [JsonPropertyName("zonesOuEdifices")]
        public string? ZonesOuEdifices { get; set; }

        [JsonPropertyName("besoinCodeAlarme")]
        public bool BesoinCodeAlarme { get; set; }

        [JsonPropertyName("detailsCodeAlarme")]
        public string? DetailsCodeAlarme { get; set; }

        [JsonPropertyName("stationnement")]
        public string? Stationnement { get; set; }

        [JsonPropertyName("puceAcces")]
        public string? PuceAcces { get; set; }

        [JsonPropertyName("freshdeskTicketId")]
        public long? FreshdeskTicketId { get; set; }
    }

    private class BadgeRequestResponse
    {
        [JsonPropertyName("requestId")]
        public string? RequestId { get; set; }
    }
}
