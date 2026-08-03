using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TremblantLifecycle.Api.Models.Entities;

namespace TremblantLifecycle.Api.Services;

/// <summary>Creates a Freshdesk ticket for every submitted request, in the "RH - Général" queue
/// (group_id/email_config_id/type confirmed against the real Freshdesk instance — see
/// tremblantsmt.freshdesk.com's /api/v2/groups, /api/v2/email_configs, /api/v2/ticket_fields).
/// This is the first of several planned system integrations on submit (TDX for IT next).</summary>
public class FreshdeskService : IFreshdeskService
{
    private readonly HttpClient _http;
    private readonly FreshdeskOptions _options;

    public FreshdeskService(HttpClient http, IOptions<FreshdeskOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task CreateTicketAsync(Request request, string requesterEmail, CancellationToken ct)
    {
        var (subject, description) = BuildContent(request);

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

        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"https://{_options.Subdomain}/api/v2/tickets")
        {
            Content = content
        };
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_options.ApiKey}:X")));

        using var response = await _http.SendAsync(requestMessage, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new FreshdeskTicketException($"Freshdesk returned {(int)response.StatusCode} {response.ReasonPhrase}: {body}");
        }
    }

    private static (string Subject, string Description) BuildContent(Request request)
    {
        var employeeNames = string.Join(", ", request.Employees.Select(e => e.NameSnapshot));
        var subject = $"{request.RequestType.ToFrenchLabel()} - {employeeNames} (#{request.RequestNumber})";

        var sb = new StringBuilder();
        sb.Append($"<h3>Demande #{request.RequestNumber} — {request.RequestType.ToFrenchLabel()}</h3>");
        sb.Append($"<p><b>Demandé par:</b> {request.CreatedByDisplayName}<br>");
        sb.Append($"<b>Date de création:</b> {request.CreatedAt:yyyy-MM-dd}</p>");

        if (request.RequestType == RequestType.Offboarding)
        {
            AppendOffboardingContent(sb, request);
        }
        else
        {
            AppendOnboardingContent(sb, request);
        }

        return (subject, sb.ToString());
    }

    private static void AppendOnboardingContent(StringBuilder sb, Request request)
    {
        var emp = request.Employees.FirstOrDefault();
        sb.Append("<h4>Employé</h4><p>");
        if (emp is not null)
        {
            sb.Append($"{emp.NameSnapshot} (#{emp.WorkdayEmployeeId})<br>");
            sb.Append($"Poste: {emp.PositionSnapshot}<br>");
            sb.Append($"Département: {emp.DepartementSnapshot}<br>");
            sb.Append($"Gestionnaire: {emp.GestionnaireSnapshot}<br>");
            sb.Append($"Type d'emploi: {emp.TypeEmploiSnapshot}");
        }
        sb.Append("</p>");

        var d = request.OnboardingDetail;
        sb.Append("<h4>Détails</h4><p>");
        sb.Append($"Date d'entrée prévue: {(d?.DateEntreePrevue is { } date ? date.ToString("yyyy-MM-dd") : "—")}<br>");
        sb.Append($"Règle de paye: {d?.RegleDePaye ?? "—"}");
        if (!string.IsNullOrWhiteSpace(d?.RegleDePayeCommentaire))
        {
            sb.Append($" — {d.RegleDePayeCommentaire}");
        }
        sb.Append("</p>");

        var a = request.AccessDetail;
        sb.Append("<h4>Accès demandés</h4><p>");
        sb.Append($"Systèmes: {JoinOrDash(a?.Systemes.Select(s => s.Value))}<br>");
        if (!string.IsNullOrWhiteSpace(a?.BadgeZones))
        {
            sb.Append($"Zones badge: {a.BadgeZones}<br>");
        }
        sb.Append($"POS/Hébergement: {JoinOrDash(a?.PosHebergement.Select(p => p.Value))}<br>");
        sb.Append($"Stationnement: {a?.Stationnement ?? "—"}<br>");
        sb.Append($"Justification: {a?.Justification ?? "—"}");
        sb.Append("</p>");

        var eq = request.EquipmentDetail;
        sb.Append("<h4>Équipement</h4><p>");
        sb.Append(JoinOrDash(eq?.Equipements.Select(x => x.Value)));
        if (!string.IsNullOrWhiteSpace(eq?.Notes))
        {
            sb.Append($"<br>Notes: {eq.Notes}");
        }
        sb.Append("</p>");

        var app = request.ApplicationsDetail;
        sb.Append("<h4>Applications</h4><p>");
        sb.Append(JoinOrDash(app?.Applications.Select(x => x.Value)));
        if (!string.IsNullOrWhiteSpace(app?.AutreLogiciel))
        {
            sb.Append($"<br>Autre logiciel: {app.AutreLogiciel}");
        }
        sb.Append("</p>");
    }

    private static void AppendOffboardingContent(StringBuilder sb, Request request)
    {
        sb.Append("<h4>Employé(s) visé(s)</h4><ul>");
        foreach (var emp in request.Employees)
        {
            sb.Append($"<li>{emp.NameSnapshot} (#{emp.WorkdayEmployeeId}) — {emp.PositionSnapshot} — {emp.DepartementSnapshot}</li>");
        }
        sb.Append("</ul>");

        var d = request.OffboardingDetail;
        sb.Append("<h4>Détails de la cessation</h4><p>");
        sb.Append($"Dernière journée: {(d?.DerniereJournee is { } date ? date.ToString("yyyy-MM-dd") : "—")}<br>");
        sb.Append($"Indemnité de vacances: {d?.IndemniteVacances ?? "—"}<br>");
        sb.Append($"Raison de l'arrêt: {d?.RaisonArret ?? "—"}<br>");
        sb.Append($"Détails: {d?.DetailsRaison ?? "—"}<br>");
        sb.Append($"Réembaucheriez-vous: {d?.Reembaucheriez ?? "—"}");
        sb.Append("</p>");

        // Deliberately the ONLY comment field included here — commentairesIT/ParkingAcces/Redingote
        // are excluded on purpose, for other systems/tickets, not this one (per explicit product
        // decision). CommentaireRH lives in a physically separate, access-restricted table in this
        // app (see OffboardingConfidentialComment's doc comment) — including it here is a deliberate
        // exception for this specific ticket type/group ("RH - Général"), not a general precedent.
        sb.Append("<h4>Commentaires RH (confidentiel)</h4><p>");
        sb.Append(string.IsNullOrWhiteSpace(request.ConfidentialComment?.CommentaireRH) ? "—" : request.ConfidentialComment.CommentaireRH);
        sb.Append("</p>");
    }

    private static string JoinOrDash(IEnumerable<string>? values)
    {
        var list = values?.ToList();
        return list is null || list.Count == 0 ? "—" : string.Join(", ", list);
    }
}
