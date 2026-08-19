using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using TremblantLifecycle.Api.Models.Entities;

namespace TremblantLifecycle.Api.Services;

public class TdxService : ITdxService
{
    /// <summary>Maps our TdxD365RoleCheckboxes long-form catalog names (used in our own admin UI for
    /// clarity) to the short labels the real TDX form uses as row names when a submission's
    /// Description table is rendered — confirmed against a real historical "D365 - Access" ticket for
    /// the Procurement roles; the rest (Accounts Payable/GL/Financial Reporting/AR) are inferred by
    /// the same "strip the section prefix and parenthetical" pattern the confirmed ones follow.</summary>
    private static readonly Dictionary<string, string> RoleShortLabels = new()
    {
        [TdxD365RoleCheckboxes.ProcurementApproverRequester] = "Approver/Requester",
        [TdxD365RoleCheckboxes.ProcurementProjectManager] = "Project Manager",
        [TdxD365RoleCheckboxes.ProcurementReceiver] = "Receiver",
        [TdxD365RoleCheckboxes.AccountsPayableAccess] = "Accounts Payable Access",
        [TdxD365RoleCheckboxes.GeneralLedgerJePreparer] = "JE Preparer / Accountant",
        [TdxD365RoleCheckboxes.GeneralLedgerJeReviewer] = "JE Reviewer / Sr. Accountant",
        [TdxD365RoleCheckboxes.FinancialReportingResortSpecific] = "Resort Specific",
        [TdxD365RoleCheckboxes.FinancialReportingDenverCorp] = "Denver/Corp",
        [TdxD365RoleCheckboxes.AccountsReceivableClerk] = "Clerk",
        [TdxD365RoleCheckboxes.AccountsReceivableManager] = "Manager",
    };
    private readonly HttpClient _http;
    private readonly TdxOptions _options;
    private readonly ITicketTemplateService _templates;
    private readonly IEmployeeFieldsService _employeeFields;

    public TdxService(HttpClient http, IOptions<TdxOptions> options, ITicketTemplateService templates, IEmployeeFieldsService employeeFields)
    {
        _http = http;
        _options = options.Value;
        _templates = templates;
        _employeeFields = employeeFields;
    }

    public async Task<int> CreateTicketAsync(Request request, RequestEmployee employee, string requesterName, string requesterEmail, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.Username) || string.IsNullOrWhiteSpace(_options.Password))
        {
            throw new TdxTicketException("TDX username/password not configured.");
        }

        var token = await GetTokenAsync(ct);
        var requesterUid = await LookupRequesterUidAsync(requesterEmail, token, ct);

        // Also sent as the ticket's Due date (EndDate below) — for Onboarding/Réactivation this is
        // "Date d'entrée prévue"; for Offboarding it's "Quelle est la dernière journée de travail de
        // l'employé", confirmed against a real test ticket (EndDate maps to the "Due" column/field).
        var isOffboarding = request.RequestType == RequestType.Offboarding;
        var effectiveDate = isOffboarding
            ? request.OffboardingDetail?.DerniereJournee
            : request.OnboardingDetail?.DateEntreePrevue;
        var effectiveDateText = effectiveDate is { } date ? date.ToString("yyyy-MM-dd") : null;

        var titleKey = isOffboarding ? TicketTemplateKeys.TdxTitleOffboarding : TicketTemplateKeys.TdxTitleOnboarding;
        var descriptionKey = isOffboarding ? TicketTemplateKeys.TdxDescriptionOffboarding : TicketTemplateKeys.TdxDescriptionOnboarding;
        var titleTemplate = await _templates.GetContentAsync(titleKey, ct);
        var descriptionTemplate = await _templates.GetContentAsync(descriptionKey, ct);

        var values = await _employeeFields.ResolveAsync(employee, ct);
        values["RequestTypeLabel"] = request.RequestType.ToFrenchLabel();
        values["DateEffective"] = effectiveDateText;

        var title = TicketTemplateRenderer.RenderInline(titleTemplate, values);
        var description = TicketTemplateRenderer.RenderInline(descriptionTemplate, values);

        var payload = new
        {
            FormID = _options.FormId,
            Title = title,
            Description = description,
            RequestorName = requesterName,
            RequestorEmail = requesterEmail,
            RequestorUid = requesterUid,
            AccountID = _options.AccountId,
            ResponsibleGroupID = _options.ResponsibleGroupId,
            ResponsibleGroupName = _options.ResponsibleGroupName,
            EndDate = effectiveDate?.ToDateTime(TimeOnly.MinValue)
        };

        return await PostTicketAsync(payload, token, ct);
    }

    /// <summary>The "D365 - Access" form (FormID 10799) doesn't use TDX custom attributes for its
    /// visible fields — a real submission bakes them all into the ticket Description as an HTML
    /// table instead (confirmed by inspecting a real historical ticket on this form). This replicates
    /// that same table structure/row order so an automated ticket looks like a manually-submitted
    /// one. Rows for optional fields (AP Access Details, Additional Legal Entities) are only included
    /// when there's a value, matching what real submissions do; unchecked roles don't get a row at
    /// all — only checked ones appear, each with value "True".</summary>
    public async Task<int> CreateD365AccessTicketAsync(D365AccessTicketInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.Username) || string.IsNullOrWhiteSpace(_options.Password))
        {
            throw new TdxTicketException("TDX username/password not configured.");
        }

        var token = await GetTokenAsync(ct);
        var requesterUid = await LookupRequesterUidAsync(input.RequesterEmail, token, ct);

        var rows = new List<(string Label, string Value)>
        {
            ("Requested on behalf of:", $"{input.RequesterName} <{input.RequesterEmail}>"),
            ("Resort", "Tremblant"),
            ("Access Type", "New Access"),
            ("Levy Employee", input.LevyEmployee ? "Yes" : "No"),
            ("User's Legal Name", input.EmployeeName),
            ("User's Email Address", input.EmployeeEmail),
        };
        if (!string.IsNullOrWhiteSpace(input.JobTitle))
        {
            rows.Add(("Job Title", input.JobTitle));
        }
        rows.Add(("Resort", "Tremblant"));
        rows.Add(("Department Number", input.DepartmentNumber));
        rows.Add(("Legal Entity", input.LegalEntity));
        if (!string.IsNullOrWhiteSpace(input.ManagerName))
        {
            rows.Add(("Manager's Name", input.ManagerName));
        }
        if (input.StartDate is { } startDate)
        {
            rows.Add(("User's Start Date", startDate.ToDateTime(TimeOnly.MinValue).ToString("dddd, MMMM d, yyyy")));
        }
        foreach (var role in input.Roles)
        {
            var shortLabel = RoleShortLabels.GetValueOrDefault(role, role);
            rows.Add((shortLabel, "True"));
        }
        if (!string.IsNullOrWhiteSpace(input.ApAccessDetails))
        {
            rows.Add(("AP Access Details", input.ApAccessDetails));
        }
        if (!string.IsNullOrWhiteSpace(input.AdditionalLegalEntities))
        {
            rows.Add(("Additional Legal Entities Needing Access To", input.AdditionalLegalEntities));
        }
        rows.Add(("Approval Limit", input.ApprovalLimit.ToString("C0", System.Globalization.CultureInfo.GetCultureInfo("en-US"))));

        var sb = new StringBuilder();
        sb.Append("<table class=\"table table-hover table-striped\">");
        sb.Append("<thead><tr><th colspan=\"2\">&nbsp;</th></tr></thead><tbody>");
        foreach (var (label, value) in rows)
        {
            sb.Append("<tr><td><strong>").Append(WebUtility.HtmlEncode(label)).Append("</strong></td><td>")
              .Append(WebUtility.HtmlEncode(value)).Append("</td></tr>");
        }
        sb.Append("</tbody></table>");

        var payload = new
        {
            FormID = _options.D365AccessFormId,
            Title = $"{input.EmployeeName} - D365 Access",
            Description = sb.ToString(),
            IsRichHtml = true,
            RequestorName = input.RequesterName,
            RequestorEmail = input.RequesterEmail,
            RequestorUid = requesterUid,
            AccountID = _options.AccountId,
            ResponsibleGroupID = _options.D365AccessResponsibleGroupId,
            ResponsibleGroupName = _options.D365AccessResponsibleGroupName
        };

        return await PostTicketAsync(payload, token, ct);
    }

    /// <summary>Fixed subject for every ticket created through the "Besoin d'aide?" form — lets IT
    /// spot at a glance that a ticket came from this app rather than a generic incident.</summary>
    private const string HelpTicketTitle = "FORMULAIRE CYCLE D'EMPLOI";

    public async Task<int> CreateHelpTicketAsync(string requesterName, string requesterEmail, string description, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.Username) || string.IsNullOrWhiteSpace(_options.Password))
        {
            throw new TdxTicketException("TDX username/password not configured.");
        }

        var token = await GetTokenAsync(ct);
        var requesterUid = await LookupRequesterUidAsync(requesterEmail, token, ct);

        var payload = new
        {
            FormID = _options.FormId,
            Title = HelpTicketTitle,
            Description = description,
            RequestorName = requesterName,
            RequestorEmail = requesterEmail,
            RequestorUid = requesterUid,
            AccountID = _options.AccountId,
            ResponsibleGroupID = _options.ResponsibleGroupId,
            ResponsibleGroupName = _options.ResponsibleGroupName,
            PriorityID = _options.HelpTicketPriorityId,
            Attributes = new[]
            {
                new { ID = _options.HelpTicketCategoryAttributeId, Value = _options.HelpTicketCategoryChoiceId.ToString() }
            }
        };

        return await PostTicketAsync(payload, token, ct);
    }

    public async Task<string?> TryLookupPersonUidAsync(string email, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(_options.Username) || string.IsNullOrWhiteSpace(_options.Password))
        {
            return null;
        }

        try
        {
            var token = await GetTokenAsync(ct);
            return await LookupRequesterUidAsync(email, token, ct);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async Task<int> PostTicketAsync(object payload, string token, CancellationToken ct)
    {
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
