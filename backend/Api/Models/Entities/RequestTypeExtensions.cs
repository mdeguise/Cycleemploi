namespace TremblantLifecycle.Api.Models.Entities;

public static class RequestTypeExtensions
{
    /// <summary>Mirrors the frontend's TypeDemande display strings (src/types.ts) — used for
    /// ticket subjects/descriptions sent to external systems (Freshdesk, TDX), not for anything
    /// shown in this app's own UI (the frontend already has its own copy of these strings).</summary>
    public static string ToFrenchLabel(this RequestType type) => type switch
    {
        RequestType.Onboarding => "Nouvelle intégration",
        RequestType.Reactivation => "Réactivation",
        RequestType.Offboarding => "Avis de terminaison ou mise à pied temporaire",
        RequestType.D365AccessOnly => "Accès D365 (demande directe)",
        _ => type.ToString()
    };
}
