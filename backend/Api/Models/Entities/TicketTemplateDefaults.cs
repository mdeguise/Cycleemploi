using System.Text.Json;

namespace TremblantLifecycle.Api.Models.Entities;

/// <summary>Fixed catalog of every admin-editable ticket template Key, its French admin-facing
/// label, the fields it exposes, its structural shape (Inline or Block — see
/// TicketTemplateShape), and its DEFAULT structured content (serialized to JSON on Content) — the
/// starting point every template ships with, so seeding these into TicketTemplates on migration is
/// a working, sensible deploy from day one.
///
/// Every ticket type is fully split by request type (Onboarding/Réactivation vs Offboarding) —
/// they never share a template, even where the default wording happens to be similar, so editing
/// one can never surprise the other. Freshdesk's two "child" tickets (fanned to two different real
/// Freshdesk groups) are also each their own template per request type.
///
/// Employee-level content (name, poste, gestionnaire, the Workday-only fields like date
/// d'embauche/centre de coûts/...) can only be placed inside a Block template's "employeeGroup"
/// block, or directly in a TDX Inline template (which always concerns exactly one employee) — never
/// as a flat request-level line in a Freshdesk body, since a termination can list several people at
/// once and a single-employee value there would be ambiguous. The employeeGroup block repeats
/// automatically once per employee on the request; the admin builds what ONE employee's lines look
/// like and never sees or configures a loop.</summary>
public static class TicketTemplateKeys
{
    public const string FreshdeskSubjectOnboarding = "FreshdeskSubjectOnboarding";
    public const string FreshdeskSubjectOffboarding = "FreshdeskSubjectOffboarding";
    public const string FreshdeskMainOnboarding = "FreshdeskMainOnboarding";
    public const string FreshdeskMainOffboarding = "FreshdeskMainOffboarding";
    public const string FreshdeskChildWithCodesOnboarding = "FreshdeskChildWithCodesOnboarding";
    public const string FreshdeskChildWithoutCodesOnboarding = "FreshdeskChildWithoutCodesOnboarding";
    public const string FreshdeskChildWithCodesOffboarding = "FreshdeskChildWithCodesOffboarding";
    public const string FreshdeskChildWithoutCodesOffboarding = "FreshdeskChildWithoutCodesOffboarding";
    public const string TdxTitleOnboarding = "TdxTitleOnboarding";
    public const string TdxTitleOffboarding = "TdxTitleOffboarding";
    public const string TdxDescriptionOnboarding = "TdxDescriptionOnboarding";
    public const string TdxDescriptionOffboarding = "TdxDescriptionOffboarding";
}

public record TicketTemplateDefinition(
    string Key,
    string Label,
    string Description,
    TicketTemplateShape Shape,
    IReadOnlyList<TicketTemplateField> RequestFields,
    bool AllowsEmployeeFields,
    string DefaultContent);

public static class TicketTemplateDefaults
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static InlinePart F(string key) => new() { Type = "field", FieldKey = key };
    private static InlinePart T(string text) => new() { Type = "text", Text = text };
    private static string Inline(params InlinePart[] parts) =>
        JsonSerializer.Serialize(new InlineTemplateContent { Parts = [.. parts] }, JsonOptions);

    private static TemplateBlock Heading(string text) => new() { Type = "heading", HeadingText = text };
    private static TemplateBlock Field(string label, string key) => new() { Type = "field", Label = label, FieldKey = key };
    private static TemplateBlock EmployeeGroup(string? heading, params (string Label, string Key)[] fields) => new()
    {
        Type = "employeeGroup",
        EmployeeGroupHeading = heading,
        EmployeeFields = [.. fields.Select(f => new EmployeeFieldLine { Label = f.Label, FieldKey = f.Key })]
    };
    private static string Block(params TemplateBlock[] blocks) =>
        JsonSerializer.Serialize(new BlockTemplateContent { Blocks = [.. blocks] }, JsonOptions);

    private static readonly IReadOnlyList<TicketTemplateField> SubjectRequestFields =
    [
        new("RequestNumber", "Numéro de la demande", TicketFieldCategory.Request),
        new("RequestTypeLabel", "Type de demande", TicketFieldCategory.Request),
        new("EmployeeNames", "Nom(s) des employé(s) visé(s), séparés par des virgules", TicketFieldCategory.Request),
    ];

    private static readonly IReadOnlyList<TicketTemplateField> OnboardingMainRequestFields =
    [
        new("RequestNumber", "Numéro de la demande", TicketFieldCategory.Request),
        new("RequestTypeLabel", "Type de demande", TicketFieldCategory.Request),
        new("RequestedBy", "Nom de la personne qui a soumis la demande", TicketFieldCategory.Request),
        new("CreatedDate", "Date de création de la demande", TicketFieldCategory.Request),
        new("DateEntreePrevue", "Date d'entrée prévue", TicketFieldCategory.Request),
        new("RegleDePaye", "Règle de paye sélectionnée", TicketFieldCategory.Request),
        new("RegleDePayeCommentaire", "Commentaire libre sur la règle de paye", TicketFieldCategory.Request),
        new("SystemesAcces", "Systèmes et accès sélectionnés", TicketFieldCategory.Request),
        new("ZonesBadge", "Zones ou édifices requis pour le badge", TicketFieldCategory.Request),
        new("PosHebergement", "Systèmes POS et hébergement sélectionnés", TicketFieldCategory.Request),
        new("Stationnement", "Stationnement requis", TicketFieldCategory.Request),
        new("JustificationAcces", "Justification des accès demandés", TicketFieldCategory.Request),
        new("CodeAlarmeDetails", "Précisions sur le code d'alarme", TicketFieldCategory.Request),
        new("Equipements", "Équipement sélectionné", TicketFieldCategory.Request),
        new("NotesEquipement", "Notes libres sur l'équipement", TicketFieldCategory.Request),
        new("Applications", "Applications sélectionnées", TicketFieldCategory.Request),
        new("AutreLogiciel", "Autre logiciel requis", TicketFieldCategory.Request),
        new("CommentairesIT", "Commentaires — technologies de l'information", TicketFieldCategory.Request),
        new("CommentairesStationnement", "Commentaires — stationnement", TicketFieldCategory.Request),
        new("CommentairesPuceAcces", "Commentaires — carte ou puce d'accès", TicketFieldCategory.Request),
        new("CommentairesRedingote", "Commentaires — uniformes et matériel à fournir", TicketFieldCategory.Request),
        // CommentaireRH lives in a physically separate, access-restricted table (see
        // OnboardingConfidentialComment's doc comment) — selectable here as the same deliberate
        // per-ticket exception offboarding's main template already has, never a general precedent.
        new("CommentaireRH", "Commentaire confidentiel des ressources humaines", TicketFieldCategory.Request),
    ];

    private static readonly IReadOnlyList<TicketTemplateField> OffboardingMainRequestFields =
    [
        new("RequestNumber", "Numéro de la demande", TicketFieldCategory.Request),
        new("RequestTypeLabel", "Type de demande", TicketFieldCategory.Request),
        new("RequestedBy", "Nom de la personne qui a soumis la demande", TicketFieldCategory.Request),
        new("CreatedDate", "Date de création de la demande", TicketFieldCategory.Request),
        new("DerniereJournee", "Dernière journée de travail", TicketFieldCategory.Request),
        new("IndemniteVacances", "Indemnité de vacances au moment de la mise à pied", TicketFieldCategory.Request),
        new("RaisonArret", "Raison de l'arrêt de travail", TicketFieldCategory.Request),
        new("DetailsRaison", "Détails sur la raison", TicketFieldCategory.Request),
        new("Reembaucheriez", "Admissibilité à la réembauche", TicketFieldCategory.Request),
        new("MotifNonAdmissibilite", "Motif de non-admissibilité (assurance-emploi)", TicketFieldCategory.Request),
        new("DateRetourConnue", "Date de retour connue (mise à pied)", TicketFieldCategory.Request),
        new("DateRetourTravail", "Date de retour au travail", TicketFieldCategory.Request),
        new("PreavisRecu", "Préavis reçu", TicketFieldCategory.Request),
        new("CommentairesIT", "Commentaires — technologies de l'information", TicketFieldCategory.Request),
        new("CommentairesStationnement", "Commentaires — stationnement", TicketFieldCategory.Request),
        new("CommentairesPuceAcces", "Commentaires — carte ou puce d'accès", TicketFieldCategory.Request),
        new("CommentairesRedingote", "Commentaires — uniformes et matériel à fournir", TicketFieldCategory.Request),
        new("CommentaireRH", "Commentaire confidentiel des ressources humaines", TicketFieldCategory.Request),
    ];

    private static readonly IReadOnlyList<TicketTemplateField> OnboardingChildRequestFields =
    [
        new("RequestTypeLabel", "Type de demande", TicketFieldCategory.Request),
        new("DateEntreePrevue", "Date d'entrée prévue", TicketFieldCategory.Request),
    ];

    private static readonly IReadOnlyList<TicketTemplateField> OffboardingChildRequestFields =
    [
        new("RequestTypeLabel", "Type de demande", TicketFieldCategory.Request),
        new("DerniereJournee", "Dernière journée de travail", TicketFieldCategory.Request),
    ];

    private static readonly IReadOnlyList<TicketTemplateField> TdxTitleRequestFields =
    [
        new("RequestTypeLabel", "Type de demande", TicketFieldCategory.Request),
        new("Applications", "Applications sélectionnées", TicketFieldCategory.Request),
        new("CommentairesIT", "Commentaires — technologies de l'information", TicketFieldCategory.Request),
        new("CommentairesStationnement", "Commentaires — stationnement", TicketFieldCategory.Request),
        new("CommentairesPuceAcces", "Commentaires — carte ou puce d'accès", TicketFieldCategory.Request),
        new("CommentairesRedingote", "Commentaires — uniformes et matériel à fournir", TicketFieldCategory.Request),
    ];

    private static readonly IReadOnlyList<TicketTemplateField> TdxDescriptionRequestFields =
    [
        new("DateEffective", "Date d'entrée prévue (Intégration/Réactivation) ou dernière journée (Terminaison)", TicketFieldCategory.Request),
        new("Applications", "Applications sélectionnées", TicketFieldCategory.Request),
        new("CommentairesIT", "Commentaires — technologies de l'information", TicketFieldCategory.Request),
        new("CommentairesStationnement", "Commentaires — stationnement", TicketFieldCategory.Request),
        new("CommentairesPuceAcces", "Commentaires — carte ou puce d'accès", TicketFieldCategory.Request),
        new("CommentairesRedingote", "Commentaires — uniformes et matériel à fournir", TicketFieldCategory.Request),
    ];

    public static readonly IReadOnlyList<TicketTemplateDefinition> All =
    [
        new(
            TicketTemplateKeys.FreshdeskSubjectOnboarding,
            "Freshdesk — Sujet (Intégration / Réactivation)",
            "Sujet du billet Freshdesk principal et de ses deux billets enfants, pour une intégration ou une réactivation.",
            TicketTemplateShape.Inline,
            SubjectRequestFields,
            AllowsEmployeeFields: false,
            Inline(F("RequestTypeLabel"), T(" - "), F("EmployeeNames"), T(" (#"), F("RequestNumber"), T(")"))),

        new(
            TicketTemplateKeys.FreshdeskSubjectOffboarding,
            "Freshdesk — Sujet (Terminaison)",
            "Sujet du billet Freshdesk principal et de ses deux billets enfants, pour un avis de terminaison ou mise à pied.",
            TicketTemplateShape.Inline,
            SubjectRequestFields,
            AllowsEmployeeFields: false,
            Inline(F("RequestTypeLabel"), T(" - "), F("EmployeeNames"), T(" (#"), F("RequestNumber"), T(")"))),

        new(
            TicketTemplateKeys.FreshdeskMainOnboarding,
            "Freshdesk — Billet principal (Intégration / Réactivation)",
            "Contenu du billet Freshdesk principal (groupe RH - Général) pour une intégration ou une réactivation.",
            TicketTemplateShape.Block,
            OnboardingMainRequestFields,
            AllowsEmployeeFields: true,
            Block(
                Field("Demandé par", "RequestedBy"),
                Field("Date de création", "CreatedDate"),
                EmployeeGroup("Employé", ("Nom", "EmployeeName"), ("Poste", "Poste"), ("Département", "Departement"), ("Gestionnaire", "Gestionnaire"), ("Type d'emploi", "TypeEmploi")),
                Heading("Détails"),
                Field("Date d'entrée prévue", "DateEntreePrevue"),
                Field("Règle de paye", "RegleDePaye"),
                Field("Commentaire règle de paye", "RegleDePayeCommentaire"),
                Heading("Accès demandés"),
                Field("Systèmes", "SystemesAcces"),
                Field("Zones badge", "ZonesBadge"),
                Field("POS/Hébergement", "PosHebergement"),
                Field("Stationnement", "Stationnement"),
                Field("Justification", "JustificationAcces"),
                Heading("Équipement"),
                Field("Équipement", "Equipements"),
                Field("Notes", "NotesEquipement"),
                Heading("Applications"),
                Field("Applications", "Applications"),
                Field("Autre logiciel", "AutreLogiciel"))),

        new(
            TicketTemplateKeys.FreshdeskMainOffboarding,
            "Freshdesk — Billet principal (Terminaison)",
            "Contenu du billet Freshdesk principal (groupe RH - Général) pour un avis de terminaison ou mise à pied.",
            TicketTemplateShape.Block,
            OffboardingMainRequestFields,
            AllowsEmployeeFields: true,
            Block(
                Field("Demandé par", "RequestedBy"),
                Field("Date de création", "CreatedDate"),
                EmployeeGroup("Employé(s) visé(s)", ("Nom", "EmployeeName"), ("Poste", "Poste"), ("Département", "Departement")),
                Heading("Détails de la cessation"),
                Field("Dernière journée", "DerniereJournee"),
                Field("Indemnité de vacances", "IndemniteVacances"),
                Field("Raison de l'arrêt", "RaisonArret"),
                Field("Détails", "DetailsRaison"),
                Field("Réembaucheriez-vous", "Reembaucheriez"),
                Heading("Commentaires RH (confidentiel)"),
                Field("Commentaire", "CommentaireRH"))),

        new(
            TicketTemplateKeys.FreshdeskChildWithCodesOnboarding,
            "Freshdesk — Billet enfant, avec codes d'emploi (Intégration / Réactivation)",
            "Billet enfant Freshdesk destiné au groupe qui a besoin de l'historique des codes d'emploi, pour une intégration ou une réactivation.",
            TicketTemplateShape.Block,
            OnboardingChildRequestFields,
            AllowsEmployeeFields: true,
            Block(
                Field("Type de demande", "RequestTypeLabel"),
                Field("Date de début prévue", "DateEntreePrevue"),
                EmployeeGroup(null, ("Nom employé", "EmployeeName"), ("Gestionnaire", "Gestionnaire"), ("Titre du poste", "Poste"), ("Groupe de paye", "PayGroup"), ("Tous les codes d'emploi", "AllJobCodes")))),

        new(
            TicketTemplateKeys.FreshdeskChildWithoutCodesOnboarding,
            "Freshdesk — Billet enfant, sans codes d'emploi (Intégration / Réactivation)",
            "Billet enfant Freshdesk destiné au groupe qui n'a pas besoin des codes d'emploi, pour une intégration ou une réactivation.",
            TicketTemplateShape.Block,
            OnboardingChildRequestFields,
            AllowsEmployeeFields: true,
            Block(
                Field("Type de demande", "RequestTypeLabel"),
                Field("Date de début prévue", "DateEntreePrevue"),
                EmployeeGroup(null, ("Nom employé", "EmployeeName"), ("Gestionnaire", "Gestionnaire"), ("Titre du poste", "Poste"), ("Groupe de paye", "PayGroup")))),

        new(
            TicketTemplateKeys.FreshdeskChildWithCodesOffboarding,
            "Freshdesk — Billet enfant, avec codes d'emploi (Terminaison)",
            "Billet enfant Freshdesk destiné au groupe qui a besoin de l'historique des codes d'emploi, pour un avis de terminaison ou mise à pied.",
            TicketTemplateShape.Block,
            OffboardingChildRequestFields,
            AllowsEmployeeFields: true,
            Block(
                Field("Type de demande", "RequestTypeLabel"),
                Field("Date de fin (dernière journée)", "DerniereJournee"),
                EmployeeGroup(null, ("Nom employé", "EmployeeName"), ("Gestionnaire", "Gestionnaire"), ("Titre du poste", "Poste"), ("Groupe de paye", "PayGroup"), ("Tous les codes d'emploi", "AllJobCodes")))),

        new(
            TicketTemplateKeys.FreshdeskChildWithoutCodesOffboarding,
            "Freshdesk — Billet enfant, sans codes d'emploi (Terminaison)",
            "Billet enfant Freshdesk destiné au groupe qui n'a pas besoin des codes d'emploi, pour un avis de terminaison ou mise à pied.",
            TicketTemplateShape.Block,
            OffboardingChildRequestFields,
            AllowsEmployeeFields: true,
            Block(
                Field("Type de demande", "RequestTypeLabel"),
                Field("Date de fin (dernière journée)", "DerniereJournee"),
                EmployeeGroup(null, ("Nom employé", "EmployeeName"), ("Gestionnaire", "Gestionnaire"), ("Titre du poste", "Poste"), ("Groupe de paye", "PayGroup")))),

        new(
            TicketTemplateKeys.TdxTitleOnboarding,
            "TDX — Titre du billet (Intégration / Réactivation)",
            "Titre du billet TDX \"Quick Incident\" (application OneIT, groupe IT Operations) pour une intégration ou une réactivation.",
            TicketTemplateShape.Inline,
            TdxTitleRequestFields,
            AllowsEmployeeFields: true,
            Inline(F("RequestTypeLabel"), T(" - "), F("EmployeeName"))),

        new(
            TicketTemplateKeys.TdxTitleOffboarding,
            "TDX — Titre du billet (Terminaison)",
            "Titre du billet TDX \"Quick Incident\" pour un avis de terminaison ou mise à pied.",
            TicketTemplateShape.Inline,
            TdxTitleRequestFields,
            AllowsEmployeeFields: true,
            Inline(F("RequestTypeLabel"), T(" - "), F("EmployeeName"))),

        new(
            TicketTemplateKeys.TdxDescriptionOnboarding,
            "TDX — Description du billet (Intégration / Réactivation)",
            "Description (texte simple, non HTML) du billet TDX \"Quick Incident\" pour une intégration ou une réactivation.",
            TicketTemplateShape.Inline,
            TdxDescriptionRequestFields,
            AllowsEmployeeFields: true,
            Inline(F("EmployeeName"), T(" - "), F("Gestionnaire"), T(" - "), F("Poste"), T(" - "), F("CodeEmploi"), T(" - "), F("DateEffective"))),

        new(
            TicketTemplateKeys.TdxDescriptionOffboarding,
            "TDX — Description du billet (Terminaison)",
            "Description (texte simple, non HTML) du billet TDX \"Quick Incident\" pour un avis de terminaison ou mise à pied.",
            TicketTemplateShape.Inline,
            TdxDescriptionRequestFields,
            AllowsEmployeeFields: true,
            Inline(F("EmployeeName"), T(" - "), F("Gestionnaire"), T(" - "), F("Poste"), T(" - "), F("CodeEmploi"), T(" - "), F("DateEffective"))),
    ];

    public static readonly IReadOnlyDictionary<string, TicketTemplateDefinition> ByKey =
        All.ToDictionary(t => t.Key);
}
