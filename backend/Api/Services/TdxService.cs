using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using TremblantLifecycle.Api.Models.Entities;

namespace TremblantLifecycle.Api.Services;

public class TdxService : ITdxService
{
    private readonly HttpClient _http;
    private readonly TdxOptions _options;

    public TdxService(HttpClient http, IOptions<TdxOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<int> CreateTicketAsync(Request request, RequestEmployee employee, string requesterName, string requesterEmail, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.Username) || string.IsNullOrWhiteSpace(_options.Password))
        {
            throw new TdxTicketException("TDX username/password not configured.");
        }

        var token = await GetTokenAsync(ct);
        var requesterUid = await LookupRequesterUidAsync(requesterEmail, token, ct);

        var isOffboarding = request.RequestType == RequestType.Offboarding;
        var effectiveDate = isOffboarding
            ? request.OffboardingDetail?.DerniereJournee
            : request.OnboardingDetail?.DateEntreePrevue;
        var effectiveDateText = effectiveDate is { } date ? date.ToString("yyyy-MM-dd") : "—";

        var payload = new
        {
            FormID = _options.FormId,
            Title = $"{request.RequestType.ToFrenchLabel()} - {employee.NameSnapshot}",
            Description = $"{employee.NameSnapshot} - {employee.GestionnaireSnapshot} - {employee.PositionSnapshot} - {employee.CodeEmploiSnapshot} - {effectiveDateText}",
            RequestorName = requesterName,
            RequestorEmail = requesterEmail,
            RequestorUid = requesterUid,
            AccountID = _options.AccountId,
            ResponsibleGroupID = _options.ResponsibleGroupId,
            ResponsibleGroupName = _options.ResponsibleGroupName
        };

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/api/{_options.AppId}/tickets")
        {
            Content = JsonContent.Create(payload)
        };
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _http.SendAsync(requestMessage, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new TdxTicketException($"TDX returned {(int)response.StatusCode} {response.ReasonPhrase}: {responseBody}");
        }

        using var doc = JsonDocument.Parse(responseBody);
        return doc.RootElement.GetProperty("ID").GetInt32();
    }

    private async Task<string> GetTokenAsync(CancellationToken ct)
    {
        using var response = await _http.PostAsJsonAsync($"{_options.BaseUrl}/api/auth", new
        {
            Username = _options.Username,
            Password = _options.Password
        }, ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new TdxTicketException($"TDX auth returned {(int)response.StatusCode} {response.ReasonPhrase}: {body}");
        }

        // Response is a raw JWT string, not JSON-wrapped (Content-Type: text/plain) — used as-is.
        return body.Trim();
    }

    private async Task<string> LookupRequesterUidAsync(string requesterEmail, string token, CancellationToken ct)
    {
        using var requestMessage = new HttpRequestMessage(
            HttpMethod.Get,
            $"{_options.BaseUrl}/api/people/lookup?searchText={Uri.EscapeDataString(requesterEmail)}");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _http.SendAsync(requestMessage, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new TdxTicketException($"TDX people lookup returned {(int)response.StatusCode} {response.ReasonPhrase}: {body}");
        }

        var people = JsonSerializer.Deserialize<List<TdxPerson>>(body, JsonOptions);
        var uid = people?.FirstOrDefault()?.Uid;
        if (string.IsNullOrWhiteSpace(uid))
        {
            throw new TdxTicketException($"TDX people lookup for '{requesterEmail}' returned no match.");
        }

        return uid;
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private class TdxPerson
    {
        [JsonPropertyName("UID")]
        public string? Uid { get; set; }
    }
}
