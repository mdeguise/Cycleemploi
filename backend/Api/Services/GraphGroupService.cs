using System.Net.Http.Json;
using Microsoft.Identity.Web;

namespace TremblantLifecycle.Api.Services;

/// <summary>Calls Microsoft Graph's checkMemberGroups on behalf of the current caller, using
/// Microsoft.Identity.Web's on-behalf-of token acquisition. Result is cached per-request only
/// (IMemoryCache with a short TTL is a reasonable Phase 2 addition if this becomes a hot path —
/// every RH-comment read currently triggers one Graph call, deliberately, to keep the authorization
/// decision authoritative rather than stale).</summary>
public class GraphGroupService : IGraphGroupService
{
    private readonly ITokenAcquisition _tokenAcquisition;
    private readonly HttpClient _httpClient;
    private static readonly string[] GraphScopes = ["https://graph.microsoft.com/.default"];

    public GraphGroupService(ITokenAcquisition tokenAcquisition, IHttpClientFactory httpClientFactory)
    {
        _tokenAcquisition = tokenAcquisition;
        _httpClient = httpClientFactory.CreateClient("Graph");
        _httpClient.BaseAddress = new Uri("https://graph.microsoft.com/v1.0/");
    }

    public async Task<bool> IsCallerInGroupAsync(string groupObjectId, CancellationToken ct = default)
    {
        var token = await _tokenAcquisition.GetAccessTokenForUserAsync(GraphScopes);

        using var request = new HttpRequestMessage(HttpMethod.Post, "me/checkMemberGroups");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new { groupIds = new[] { groupObjectId } });

        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CheckMemberGroupsResponse>(cancellationToken: ct);
        return result?.Value?.Contains(groupObjectId) ?? false;
    }

    private class CheckMemberGroupsResponse
    {
        public List<string>? Value { get; set; }
    }
}
