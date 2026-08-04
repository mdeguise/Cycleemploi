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

    public async Task<string> CreateBadgeRequestAsync(Request request, RequestEmployee employee, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.BadgeRequestWebhookUrl))
        {
            throw new DynamicsEamException("Power Automate badge-request webhook URL not configured.");
        }

        var access = request.AccessDetail;
        var d = request.OnboardingDetail;
        var systemes = access?.Systemes.Select(s => s.Value).ToList() ?? [];

        var payload = new BadgeRequestPayload
        {
            RequestNumber = request.RequestNumber,
            NomEmploye = employee.NameSnapshot,
            PositionTitle = employee.PositionSnapshot,
            Departement = employee.DepartementSnapshot,
            Statut = employee.TypeEmploiSnapshot,
            DebutantLe = d?.DateEntreePrevue?.ToString("yyyy-MM-dd"),
            Manager = employee.GestionnaireSnapshot,
            DemandePar = request.CreatedByDisplayName,
            ZonesOuEdifices = access?.BadgeZones,
            BesoinCodeAlarme = systemes.Contains("Besoin de code d'alarme"),
            DetailsCodeAlarme = access?.CodeAlarmeDetails
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

        [JsonPropertyName("nomEmploye")]
        public string? NomEmploye { get; set; }

        [JsonPropertyName("positionTitle")]
        public string? PositionTitle { get; set; }

        [JsonPropertyName("departement")]
        public string? Departement { get; set; }

        [JsonPropertyName("statut")]
        public string? Statut { get; set; }

        [JsonPropertyName("debutantLe")]
        public string? DebutantLe { get; set; }

        [JsonPropertyName("manager")]
        public string? Manager { get; set; }

        [JsonPropertyName("demandePar")]
        public string? DemandePar { get; set; }

        [JsonPropertyName("zonesOuEdifices")]
        public string? ZonesOuEdifices { get; set; }

        [JsonPropertyName("besoinCodeAlarme")]
        public bool BesoinCodeAlarme { get; set; }

        [JsonPropertyName("detailsCodeAlarme")]
        public string? DetailsCodeAlarme { get; set; }
    }

    private class BadgeRequestResponse
    {
        [JsonPropertyName("requestId")]
        public string? RequestId { get; set; }
    }
}
