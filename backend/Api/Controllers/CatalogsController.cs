using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TremblantLifecycle.Api.Models.Dtos;

namespace TremblantLifecycle.Api.Controllers;

[ApiController]
[Route("api/catalogs")]
[Authorize]
public class CatalogsController : ControllerBase
{
    // Values mirror src/data/catalogs.ts exactly. These are "fictional"/placeholder catalog values
    // per the app's design history — not sourced from any external system — so backend constants
    // are the correct home for them (see plan's API design: move to DB-backed tables later only if
    // the business needs to edit these without a deploy).
    private static readonly CatalogsDto Catalogs = new()
    {
        Departements =
        [
            "Opérations montagne", "Hébergement", "Ventes et marketing", "Ressources humaines",
            "Finances", "Technologies de l'information", "Restauration", "École de ski"
        ],
        TypesEmploi = ["Temps plein - permanent", "Temps plein - saisonnier", "Temps partiel", "Contractuel"],
        RegleDePayeAutre = "AUTRES PRÉCISÉ DANS COMMENTAIRES",
        ReglesDePaye =
        [
            "05H45 SANS REPAS", "7H30 AVEC 60 MIN DE REPAS", "7H30 AVEC 30 MIN DE REPAS",
            "8h SANS REPAS", "8H AVEC 30 MINUTES REPAS", "10H SANS REPAS", "10H AVEC 30 MINUTES REPAS",
            "AUTRES PRÉCISÉ DANS COMMENTAIRES"
        ],
        SystemesAcces =
        [
            new AccessSystemDto { Nom = "Compte Active Directory / courriel", Description = "Compte réseau et boîte courriel @tremblant.ca" },
            new AccessSystemDto { Nom = "Accès VPN", Description = "Accès à distance au réseau corporatif" },
            new AccessSystemDto { Nom = "Badge d'accès aux édifices", Description = "Accès physique aux bureaux et installations" }
        ],
        PosHebergementSystemes = ["RTP", "SMS", "OPERA", "SYMPHONIE", "APROPOS"],
        Equipements =
        [
            new EquipmentItemDto { Nom = "Ordinateur portable", Categorie = "Informatique" },
            new EquipmentItemDto { Nom = "Ordinateur de bureau", Categorie = "Informatique" },
            new EquipmentItemDto { Nom = "Écran additionnel", Categorie = "Informatique" },
            new EquipmentItemDto { Nom = "Téléphone cellulaire", Categorie = "Télécommunications" },
            new EquipmentItemDto { Nom = "Radio bidirectionnelle", Categorie = "Télécommunications" },
            new EquipmentItemDto { Nom = "Uniforme / vêtements corporatifs", Categorie = "Équipement de travail" },
            new EquipmentItemDto { Nom = "Laissez-passer de saison", Categorie = "Équipement de travail" }
        ],
        Applications =
        [
            new ApplicationItemDto { Nom = "Microsoft 365", Editeur = "Microsoft" },
            new ApplicationItemDto { Nom = "Teams", Editeur = "Microsoft" },
            new ApplicationItemDto { Nom = "Dynamics 365", Editeur = "Microsoft" }
        ],
        OuiNon = ["Oui", "Non"],
        RaisonsArret =
        [
            "Fin de saison / mise à pied saisonnière", "Mise à pied temporaire (manque de travail)",
            "Démission volontaire", "Congédiement", "Fin de contrat", "Retraite", "Autre"
        ],
        ReembaucheriezOptions = ["Oui", "Non", "À déterminer"]
    };

    [HttpGet]
    public ActionResult<CatalogsDto> Get() => Ok(Catalogs);
}
