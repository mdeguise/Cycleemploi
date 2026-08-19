namespace TremblantLifecycle.Api.Models.Entities;

/// <summary>One available {{Placeholder}} for a given template, shown to the admin as a reference
/// alongside the editable text box.</summary>
public record TicketTemplatePlaceholder(string Name, string Description);

public record TicketTemplateDefinition(string Key, string Label, string Description, string DefaultContent, IReadOnlyList<TicketTemplatePlaceholder> Placeholders);

/// <summary>Fixed catalog of every admin-editable ticket template Key, its French admin-facing
/// label, its available placeholders, and its DEFAULT content — the exact wording this app sent
/// before this feature existed (see FreshdeskService/TdxService git history), so seeding these
/// defaults into TicketTemplates on migration is a no-behavior-change deploy. Two placeholders
/// ("EmployeesListe" on FreshdeskMainOffboarding, "EmployeesDetailBloc" on the two Freshdesk child
/// templates) are pre-rendered HTML blocks built server-side — one entry per employee on the
/// request — because their per-employee formatting depends on an async Workday lookup (pay group,
/// job codes) that can't be reduced to a flat placeholder; the admin can reposition the whole block
/// but not restyle individual employee lines.</summary>
public static class TicketTemplateKeys
{
    public const string FreshdeskSubject = "FreshdeskSubject";
    public const string FreshdeskMainOnboarding = "FreshdeskMainOnboarding";
    public const string FreshdeskMainOffboarding = "FreshdeskMainOffboarding";
    public const string FreshdeskChildWithJobCodes = "FreshdeskChildWithJobCodes";
    public const string FreshdeskChildWithoutJobCodes = "FreshdeskChildWithoutJobCodes";
    public const string TdxQuickIncidentTitle = "TdxQuickIncidentTitle";
    public const string TdxQuickIncidentDescription = "TdxQuickIncidentDescription";
}

public static class TicketTemplateDefaults
{
    public static readonly IReadOnlyList<TicketTemplateDefinition> All =
    [
        new(
            TicketTemplateKeys.FreshdeskSubject,
            "Freshdesk — Sujet (billet principal et billets enfants)",
            "Sujet utilisé pour le billet Freshdesk principal (RH - Général) et ses deux billets enfants.",
            "{{RequestTypeLabel}} - {{EmployeeNames}} (#{{RequestNumber}})",
            [
                new("RequestTypeLabel", "Type de demande (ex. Nouvelle intégration)"),
                new("EmployeeNames", "Nom(s) des employé(s) visé(s), séparés par des virgules"),
                new("RequestNumber", "Numéro de la demande (ex. INT-2026-00024)"),
            ]),

        new(
            TicketTemplateKeys.FreshdeskMainOnboarding,
            "Freshdesk — Billet principal (Intégration / Réactivation)",
            "Contenu du billet Freshdesk principal (groupe RH - Général) pour une intégration ou une réactivation.",
            "<h3>Demande #{{RequestNumber}} — {{RequestTypeLabel}}</h3>\n" +
            "<p><b>Demandé par:</b> {{RequestedBy}}<br>\n" +
            "<b>Date de création:</b> {{CreatedDate}}</p>\n" +
            "<h4>Employé</h4><p>\n" +
            "{{EmployeeName}} (#{{EmployeeId}})<br>\n" +
            "Poste: {{Poste}}<br>\n" +
            "Département: {{Departement}}<br>\n" +
            "Gestionnaire: {{Gestionnaire}}<br>\n" +
            "Type d'emploi: {{TypeEmploi}}\n" +
            "</p>\n" +
            "<h4>Détails</h4><p>\n" +
            "Date d'entrée prévue: {{DateEntreePrevue}}<br>\n" +
            "Règle de paye: {{RegleDePaye}}<br>\n" +
            "Commentaire règle de paye: {{RegleDePayeCommentaire}}\n" +
            "</p>\n" +
            "<h4>Accès demandés</h4><p>\n" +
            "Systèmes: {{SystemesAcces}}<br>\n" +
            "Zones badge: {{ZonesBadge}}<br>\n" +
            "POS/Hébergement: {{PosHebergement}}<br>\n" +
            "Stationnement: {{Stationnement}}<br>\n" +
            "Justification: {{JustificationAcces}}\n" +
            "</p>\n" +
            "<h4>Équipement</h4><p>\n" +
            "{{Equipements}}<br>\n" +
            "Notes: {{NotesEquipement}}\n" +
            "</p>\n" +
            "<h4>Applications</h4><p>\n" +
            "{{Applications}}<br>\n" +
            "Autre logiciel: {{AutreLogiciel}}\n" +
            "</p>",
            [
                new("RequestNumber", "Numéro de la demande"),
                new("RequestTypeLabel", "Type de demande"),
                new("RequestedBy", "Nom de la personne qui a soumis la demande"),
                new("CreatedDate", "Date de création de la demande (aaaa-mm-jj)"),
                new("EmployeeName", "Nom complet de l'employé"),
                new("EmployeeId", "Numéro d'employé Workday"),
                new("Poste", "Titre du poste"),
                new("Departement", "Département"),
                new("Gestionnaire", "Nom du gestionnaire"),
                new("TypeEmploi", "Type d'emploi"),
                new("DateEntreePrevue", "Date d'entrée prévue (aaaa-mm-jj)"),
                new("RegleDePaye", "Règle de paye sélectionnée"),
                new("RegleDePayeCommentaire", "Commentaire libre sur la règle de paye"),
                new("SystemesAcces", "Systèmes et accès sélectionnés, séparés par des virgules"),
                new("ZonesBadge", "Zones ou édifices requis pour le badge"),
                new("PosHebergement", "Systèmes POS et hébergement sélectionnés"),
                new("Stationnement", "Stationnement requis"),
                new("JustificationAcces", "Justification des accès demandés"),
                new("Equipements", "Équipement sélectionné, séparé par des virgules"),
                new("NotesEquipement", "Notes libres sur l'équipement"),
                new("Applications", "Applications sélectionnées, séparées par des virgules"),
                new("AutreLogiciel", "Autre logiciel requis (texte libre)"),
            ]),

        new(
            TicketTemplateKeys.FreshdeskMainOffboarding,
            "Freshdesk — Billet principal (Terminaison)",
            "Contenu du billet Freshdesk principal (groupe RH - Général) pour un avis de terminaison ou mise à pied.",
            "<h3>Demande #{{RequestNumber}} — {{RequestTypeLabel}}</h3>\n" +
            "<p><b>Demandé par:</b> {{RequestedBy}}<br>\n" +
            "<b>Date de création:</b> {{CreatedDate}}</p>\n" +
            "<h4>Employé(s) visé(s)</h4><ul>{{EmployeesListe}}</ul>\n" +
            "<h4>Détails de la cessation</h4><p>\n" +
            "Dernière journée: {{DerniereJournee}}<br>\n" +
            "Indemnité de vacances: {{IndemniteVacances}}<br>\n" +
            "Raison de l'arrêt: {{RaisonArret}}<br>\n" +
            "Détails: {{DetailsRaison}}<br>\n" +
            "Réembaucheriez-vous: {{Reembaucheriez}}\n" +
            "</p>\n" +
            "<h4>Commentaires RH (confidentiel)</h4><p>{{CommentaireRH}}</p>",
            [
                new("RequestNumber", "Numéro de la demande"),
                new("RequestTypeLabel", "Type de demande"),
                new("RequestedBy", "Nom de la personne qui a soumis la demande"),
                new("CreatedDate", "Date de création de la demande (aaaa-mm-jj)"),
                new("EmployeesListe", "Bloc pré-formaté : un <li> par employé visé (nom, numéro, poste, département) — ne peut pas être personnalisé par employé individuellement, seulement repositionné"),
                new("DerniereJournee", "Dernière journée de travail (aaaa-mm-jj)"),
                new("IndemniteVacances", "Indemnité de vacances au moment de la mise à pied"),
                new("RaisonArret", "Raison de l'arrêt de travail"),
                new("DetailsRaison", "Détails sur la raison"),
                new("Reembaucheriez", "Admissibilité à la réembauche"),
                new("CommentaireRH", "Commentaire confidentiel des ressources humaines"),
            ]),

        new(
            TicketTemplateKeys.FreshdeskChildWithJobCodes,
            "Freshdesk — Billet enfant (avec codes d'emploi)",
            "Contenu du billet Freshdesk enfant destiné au groupe incluant la liste complète des codes d'emploi de chaque employé.",
            "<h3>Demande #{{RequestNumber}} — {{RequestTypeLabel}}</h3>\n" +
            "<p><b>Type de demande:</b> {{RequestTypeLabel}}</p>\n" +
            "{{EmployeesDetailBloc}}",
            [
                new("RequestNumber", "Numéro de la demande"),
                new("RequestTypeLabel", "Type de demande"),
                new("EmployeesDetailBloc", "Bloc pré-formaté : un paragraphe par employé visé (nom, date, gestionnaire, poste, groupe de paye, tous les codes d'emploi) — ne peut pas être personnalisé par employé individuellement, seulement repositionné"),
            ]),

        new(
            TicketTemplateKeys.FreshdeskChildWithoutJobCodes,
            "Freshdesk — Billet enfant (sans codes d'emploi)",
            "Contenu du billet Freshdesk enfant destiné au groupe qui n'a pas besoin des codes d'emploi.",
            "<h3>Demande #{{RequestNumber}} — {{RequestTypeLabel}}</h3>\n" +
            "<p><b>Type de demande:</b> {{RequestTypeLabel}}</p>\n" +
            "{{EmployeesDetailBloc}}",
            [
                new("RequestNumber", "Numéro de la demande"),
                new("RequestTypeLabel", "Type de demande"),
                new("EmployeesDetailBloc", "Bloc pré-formaté : un paragraphe par employé visé (nom, date, gestionnaire, poste, groupe de paye) — ne peut pas être personnalisé par employé individuellement, seulement repositionné"),
            ]),

        new(
            TicketTemplateKeys.TdxQuickIncidentTitle,
            "TDX — Titre du billet (Quick Incident)",
            "Titre du billet TDX \"Quick Incident\" (application OneIT, groupe IT Operations) créé pour chaque demande.",
            "{{RequestTypeLabel}} - {{EmployeeName}}",
            [
                new("RequestTypeLabel", "Type de demande"),
                new("EmployeeName", "Nom complet de l'employé"),
            ]),

        new(
            TicketTemplateKeys.TdxQuickIncidentDescription,
            "TDX — Description du billet (Quick Incident)",
            "Description (texte simple, non HTML) du billet TDX \"Quick Incident\".",
            "{{EmployeeName}} - {{Gestionnaire}} - {{Poste}} - {{CodeEmploi}} - {{DateEffective}}",
            [
                new("EmployeeName", "Nom complet de l'employé"),
                new("Gestionnaire", "Nom du gestionnaire"),
                new("Poste", "Titre du poste"),
                new("CodeEmploi", "Code d'emploi Workday"),
                new("DateEffective", "Date d'entrée prévue (Intégration/Réactivation) ou dernière journée (Terminaison), aaaa-mm-jj"),
            ]),
    ];

    public static readonly IReadOnlyDictionary<string, TicketTemplateDefinition> ByKey =
        All.ToDictionary(t => t.Key);
}
