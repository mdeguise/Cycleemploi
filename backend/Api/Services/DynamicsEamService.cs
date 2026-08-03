using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using TremblantLifecycle.Api.Models.Entities;

namespace TremblantLifecycle.Api.Services;

public class DynamicsEamService : IDynamicsEamService
{
    // WREF0000 (8 chars) + a 5-digit sequence number = 13-char RequestId, matching the numbering
    // scheme the original Power Automate flow used (and that other, still-active D365 processes
    // presumably still expect).
    private const string RequestIdPrefix = "WREF0000";
    private const int MaxCreateAttempts = 20;

    private readonly HttpClient _http;
    private readonly DynamicsOptions _options;

    public DynamicsEamService(HttpClient http, IOptions<DynamicsOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<string> CreateBadgeRequestAsync(Request request, RequestEmployee employee, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.ClientSecret))
        {
            throw new DynamicsEamException("D365 client secret not configured.");
        }

        var token = await GetAccessTokenAsync(ct);

        var access = request.AccessDetail;
        var d = request.OnboardingDetail;
        var description = $"ACTIVATION CARTE A PUCE ou ALARME POUR {employee.NameSnapshot}";
        var notes =
            $"DEMANDE CYCLE EMPLOI #{request.RequestNumber}\n\n" +
            $"NOM EMPLOYÉ: {employee.NameSnapshot}\n" +
            $"DÉPARTEMENT: {employee.DepartementSnapshot}\n" +
            $"STATUT: {employee.TypeEmploiSnapshot}\n" +
            $"DÉBUTANT LE: {(d?.DateEntreePrevue is { } date ? date.ToString("yyyy-MM-dd") : "—")}\n" +
            $"MANAGER: {employee.GestionnaireSnapshot}\n" +
            $"ZONES OU ÉDIFICES REQUIS: {access?.BadgeZones ?? "—"}\n" +
            $"BESOIN CODE ALARME: {(access?.BesoinCodeAlarme == true ? "Oui" : "Non")}";

        var nextId = await GetNextRequestIdAsync(token, ct);

        for (var attempt = 1; attempt <= MaxCreateAttempts; attempt++)
        {
            var requestId = RequestIdPrefix + nextId.ToString("D5");

            using var createRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/data/AssetMaintenanceRequests")
            {
                Content = JsonContent.Create(new
                {
                    RequestId = requestId,
                    RequestTypeId = _options.RequestTypeId,
                    dataAreaId = _options.DataAreaId,
                    Description = description,
                    FunctionalLocationId = _options.FunctionalLocationId,
                    Notes = notes
                })
            };
            createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _http.SendAsync(createRequest, ct);
            if (response.IsSuccessStatusCode)
            {
                return requestId;
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            var looksLikeDuplicateKey = body.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
                || body.Contains("already exists", StringComparison.OrdinalIgnoreCase);

            if (!looksLikeDuplicateKey || attempt == MaxCreateAttempts)
            {
                throw new DynamicsEamException($"D365 returned {(int)response.StatusCode} {response.ReasonPhrase} creating {requestId}: {body}");
            }

            nextId++;
        }

        throw new DynamicsEamException($"Exhausted {MaxCreateAttempts} attempts trying to create an AssetMaintenanceRequests record — RequestId numbering may be badly out of sync.");
    }

    private async Task<int> GetNextRequestIdAsync(string token, CancellationToken ct)
    {
        using var listRequest = new HttpRequestMessage(HttpMethod.Get,
            $"{_options.BaseUrl}/data/AssetMaintenanceRequests?$select=RequestId&$filter=startswith(RequestId,'{RequestIdPrefix}')");
        listRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _http.SendAsync(listRequest, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new DynamicsEamException($"D365 returned {(int)response.StatusCode} {response.ReasonPhrase} listing AssetMaintenanceRequests: {body}");
        }

        var result = await response.Content.ReadFromJsonAsync<ODataListResponse<RequestIdRow>>(cancellationToken: ct);
        var lastFive = result?.Value
            .Where(r => r.RequestId.Length == RequestIdPrefix.Length + 5)
            .Select(r => r.RequestId[^5..])
            .Where(s => int.TryParse(s, out _))
            .Select(int.Parse)
            .DefaultIfEmpty(0)
            .Max() ?? 0;

        return lastFive + 1;
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, $"https://login.microsoftonline.com/{_options.TenantId}/oauth2/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["resource"] = _options.BaseUrl
            })
        };

        using var response = await _http.SendAsync(tokenRequest, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new DynamicsEamException($"Entra ID token request returned {(int)response.StatusCode} {response.ReasonPhrase}: {body}");
        }

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct);
        return token?.AccessToken ?? throw new DynamicsEamException("Entra ID token response did not contain an access_token.");
    }

    private class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = "";
    }

    private class ODataListResponse<T>
    {
        [JsonPropertyName("value")]
        public List<T> Value { get; set; } = [];
    }

    private class RequestIdRow
    {
        public string RequestId { get; set; } = "";
    }
}
