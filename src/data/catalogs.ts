export const DEPARTEMENTS = [
  'Opérations montagne',
  'Hébergement',
  'Ventes et marketing',
  'Ressources humaines',
  'Finances',
  'Technologies de l\'information',
  'Restauration',
  'École de ski',
];

export const TYPES_EMPLOI = ['Temps plein - permanent', 'Temps plein - saisonnier', 'Temps partiel', 'Contractuel'];

export const REGLE_DE_PAYE_AUTRE = 'AUTRES PRÉCISÉ DANS COMMENTAIRES';

/** Employees in this Workday Pay_Group don't need to answer "Règle de paye" — mirrored on the
 * backend in RequestsController's PayGroupNonUnion constant. */
export const PAY_GROUP_NON_UNION = 'CAN Tremblant-Non Union';

export const REGLES_DE_PAYE = [
  'AUCUNE',
  '05H45 SANS REPAS',
  '7H30 AVEC 60 MIN DE REPAS',
  '7H30 AVEC 30 MIN DE REPAS',
  '8h SANS REPAS',
  '8H AVEC 30 MINUTES REPAS',
  '10H SANS REPAS',
  '10H AVEC 30 MINUTES REPAS',
  REGLE_DE_PAYE_AUTRE,
];

// Display-only breakdown of each REGLES_DE_PAYE value into hours/meal-break, for the icon-based
// dropdown (RegleDePayeSelect) — the underlying stored value stays the exact catalog string above,
// this is purely presentational. AUCUNE/REGLE_DE_PAYE_AUTRE deliberately have no entry (rendered as
// plain text, no icons).
export const REGLE_DE_PAYE_DISPLAY: Record<string, { hours: string; repas: string }> = {
  '05H45 SANS REPAS': { hours: '5 h 45', repas: 'Aucun repas' },
  '7H30 AVEC 60 MIN DE REPAS': { hours: '7 h 30', repas: '60 min' },
  '7H30 AVEC 30 MIN DE REPAS': { hours: '7 h 30', repas: '30 min' },
  '8h SANS REPAS': { hours: '8 h 00', repas: 'Aucun repas' },
  '8H AVEC 30 MINUTES REPAS': { hours: '8 h 00', repas: '30 min' },
  '10H SANS REPAS': { hours: '10 h 00', repas: 'Aucun repas' },
  '10H AVEC 30 MINUTES REPAS': { hours: '10 h 00', repas: '30 min' },
};


// No separate `id` field — `nom` is the identifier, matching the backend's junction tables
// (RequestAccessSysteme etc.), which store the display text directly rather than a code (see
// backend/Api/Models/Entities/AccessDetail.cs).
export interface AccessSystem {
  nom: string;
  description: string;
}

export const SYSTEMES_ACCES: AccessSystem[] = [
  {
    nom: 'Compte Active Directory / courriel',
    description: 'Création du compte réseau, de la boîte courriel et des accès Microsoft 365.',
  },
  { nom: 'Accès VPN', description: "Permet l'accès sécurisé au réseau corporatif à partir de l'extérieur de l'entreprise." },
  { nom: 'Badge d\'accès aux édifices', description: "Carte et puce d'accès aux bureaux et bâtiments autorisés." },
  { nom: 'Besoin de code d\'alarme', description: 'Créer un code d\'alarme individuel pour cet employé.' },
  { nom: 'Accès D365', description: 'Accès à Dynamics 365 (Comptes fournisseurs, Grand livre, Comptes clients, Approvisionnement)' },
];

export const ACCES_BADGE = 'Badge d\'accès aux édifices';
export const BESOIN_CODE_ALARME = 'Besoin de code d\'alarme';
export const ACCES_D365 = 'Accès D365';

export interface PosHebergementSysteme {
  nom: string;
  description: string;
  facultatif?: boolean;
}

export const POS_HEBERGEMENT_SYSTEMES: PosHebergementSysteme[] = [
  {
    nom: 'RTP',
    description: 'Système de point de vente (POS), boutiques, location, service à la clientèle, golf.',
    facultatif: true,
  },
  { nom: 'SMS', description: 'Système de point de vente (POS) hébergement et des réservations des Suites Tremblant.' },
  { nom: 'OPERA', description: 'Système de point de vente (POS) hébergement et des réservations du Holiday Inn Express.' },
  { nom: 'APROPOS', description: 'Gestion des ventes au détail et de l\'inventaire.' },
];

export interface EquipmentItem {
  nom: string;
  categorie: string;
  description?: string;
}

export const EQUIPEMENTS: EquipmentItem[] = [
  { nom: 'Ordinateur portable', categorie: 'Informatique' },
  { nom: 'Ordinateur de bureau', categorie: 'Informatique' },
  { nom: 'Écran additionnel', categorie: 'Informatique' },
  { nom: 'Téléphone cellulaire', categorie: 'Télécommunications', description: 'Téléphone mobile' },
  { nom: 'Radio bidirectionnelle', categorie: 'Télécommunications', description: 'Radio portative' },
  { nom: 'Uniforme / vêtements corporatifs', categorie: 'Équipement de travail' },
  { nom: 'Laissez-passer de saison', categorie: 'Équipement de travail' },
];

export const CATEGORIE_TELECOMMUNICATIONS_DESCRIPTION = 'Téléphonie et communications';

export interface ApplicationItem {
  nom: string;
  editeur: string;
  description: string;
}

export const APPLICATIONS: ApplicationItem[] = [
  { nom: 'Microsoft 365', editeur: 'Microsoft', description: 'Courriel Outlook, OneDrive, Word, Excel et Power Point' },
  { nom: 'Teams', editeur: 'Microsoft', description: 'Messagerie, réunions et collaboration' },
  { nom: 'Dynaway', editeur: 'Dynaway', description: 'Gestion des actifs et de la maintenance (EAM) — requiert automatiquement un accès D365.' },
];

/** Dynamics 365 itself is requested via "Accès D365" in the Access section (SYSTEMES_ACCES,
 * above) — that's the checkbox wired to the real D365 access-approval workflow (see backend's
 * TryCreateD365AccessApprovalRequestAsync). It's deliberately not listed here too, so there's
 * only one place to request it. */

/** Selecting this application implicitly requires D365 access — see Step3Access, which locks
 * ACCES_D365 checked while this stays selected. */
export const DYNAWAY = 'Dynaway';

export const OUI_NON = ['Oui', 'Non'];

export const RAISON_ARRET_MISE_A_PIED_TEMPORAIRE = 'Mise à pied temporaire (manque de travail)';
export const RAISON_ARRET_DEMISSION_VOLONTAIRE = 'Démission volontaire';

export const RAISONS_ARRET = [
  'Fin de saison / mise à pied saisonnière',
  RAISON_ARRET_MISE_A_PIED_TEMPORAIRE,
  RAISON_ARRET_DEMISSION_VOLONTAIRE,
  'Congédiement',
  'Fin de contrat',
  'Retraite',
  'Autre',
];

export const REEMBAUCHERIEZ_NON = 'Non';
export const REEMBAUCHERIEZ_OPTIONS = ['Oui', REEMBAUCHERIEZ_NON, 'À déterminer'];
