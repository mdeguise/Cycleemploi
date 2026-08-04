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

export const REGLES_DE_PAYE = [
  '05H45 SANS REPAS',
  '7H30 AVEC 60 MIN DE REPAS',
  '7H30 AVEC 30 MIN DE REPAS',
  '8h SANS REPAS',
  '8H AVEC 30 MINUTES REPAS',
  '10H SANS REPAS',
  '10H AVEC 30 MINUTES REPAS',
  REGLE_DE_PAYE_AUTRE,
];


// No separate `id` field — `nom` is the identifier, matching the backend's junction tables
// (RequestAccessSysteme etc.), which store the display text directly rather than a code (see
// backend/Api/Models/Entities/AccessDetail.cs).
export interface AccessSystem {
  nom: string;
  description: string;
}

export const SYSTEMES_ACCES: AccessSystem[] = [
  { nom: 'Compte Active Directory / courriel', description: 'Compte réseau et boîte courriel @tremblant.ca' },
  { nom: 'Accès VPN', description: 'Accès à distance au réseau corporatif' },
  { nom: 'Badge d\'accès aux édifices', description: 'Accès physique aux bureaux et installations' },
  { nom: 'Besoin de code d\'alarme', description: 'Un code d\'alarme doit être créé pour cet employé' },
];

export const ACCES_BADGE = 'Badge d\'accès aux édifices';
export const BESOIN_CODE_ALARME = 'Besoin de code d\'alarme';

export const POS_HEBERGEMENT_SYSTEMES = ['RTP', 'SMS', 'OPERA', 'SYMPHONIE', 'APROPOS'];

export interface EquipmentItem {
  nom: string;
  categorie: string;
}

export const EQUIPEMENTS: EquipmentItem[] = [
  { nom: 'Ordinateur portable', categorie: 'Informatique' },
  { nom: 'Ordinateur de bureau', categorie: 'Informatique' },
  { nom: 'Écran additionnel', categorie: 'Informatique' },
  { nom: 'Téléphone cellulaire', categorie: 'Télécommunications' },
  { nom: 'Radio bidirectionnelle', categorie: 'Télécommunications' },
  { nom: 'Uniforme / vêtements corporatifs', categorie: 'Équipement de travail' },
  { nom: 'Laissez-passer de saison', categorie: 'Équipement de travail' },
];

export interface ApplicationItem {
  nom: string;
  editeur: string;
}

export const APPLICATIONS: ApplicationItem[] = [
  { nom: 'Microsoft 365', editeur: 'Microsoft' },
  { nom: 'Teams', editeur: 'Microsoft' },
  { nom: 'Dynamics 365', editeur: 'Microsoft' },
];

export const OUI_NON = ['Oui', 'Non'];

export const RAISONS_ARRET = [
  'Fin de saison / mise à pied saisonnière',
  'Mise à pied temporaire (manque de travail)',
  'Démission volontaire',
  'Congédiement',
  'Fin de contrat',
  'Retraite',
  'Autre',
];

export const REEMBAUCHERIEZ_OPTIONS = ['Oui', 'Non', 'À déterminer'];
