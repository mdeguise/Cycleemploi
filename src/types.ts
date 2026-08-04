export type RequestStatus = 'Brouillon' | 'Soumise' | 'En traitement' | 'Complétée';

export const TYPE_DEMANDE_TERMINAISON = 'Avis de terminaison ou mise à pied temporaire';

export type TypeDemande = 'Nouvelle intégration' | 'Réactivation' | typeof TYPE_DEMANDE_TERMINAISON | '';

/** Denormalized snapshot captured at selection time — mirrors backend RequestEmployeeDto. Not a
 * live reference to WorkdayDemographic, which reloads on its own hourly schedule outside this
 * app's control (see backend/Api/Models/Entities/RequestEmployee.cs). */
export interface EmployeeSnapshot {
  workdayEmployeeId: string;
  nom: string;
  prenom: string;
  numeroEmploye: string;
  poste: string;
  departement: string;
  codeEmploi: string;
  typeEmploi: string;
  gestionnaire: string;
}

export interface EmployeeSelectionInfo {
  employee: EmployeeSnapshot | null;
  dateEntreePrevue: string;
  regleDePaye: string;
  regleDePayeCommentaire: string;
}

export interface AccessInfo {
  systemes: string[];
  posHebergement: string[];
  badgeZones: string;
  codeAlarmeDetails: string;
  justification: string;
  stationnement: string;
}

export interface EquipmentInfo {
  equipements: string[];
  notes: string;
}

export interface ApplicationsInfo {
  applications: string[];
  autreLogiciel: string;
}

export interface OffboardingInfo {
  employees: EmployeeSnapshot[];
  derniereJournee: string;
  indemniteVacances: string;
  raisonArret: string;
  detailsRaison: string;
  reembaucheriez: string;
  attachments: File[];
  commentairesRH: string;
  commentairesIT: string;
  commentairesParkingAcces: string;
  commentairesRedingote: string;
}

export interface OnboardingRequest {
  /** Null until setTypeDemande's first call creates the backend record — see WizardContext. Every
   * field below is purely local/optimistic state until then. */
  requestId: number | null;
  demandeNumero: string;
  dateCreation: string;
  demandePar: string;
  statut: RequestStatus;
  typeDemande: TypeDemande;
  employee: EmployeeSelectionInfo;
  access: AccessInfo;
  equipment: EquipmentInfo;
  applications: ApplicationsInfo;
  offboarding: OffboardingInfo;
}

export function createEmptyRequest(demandePar: string): OnboardingRequest {
  return {
    requestId: null,
    demandeNumero: '',
    dateCreation: new Date().toISOString().slice(0, 10),
    demandePar,
    statut: 'Brouillon',
    typeDemande: '',
    employee: {
      employee: null,
      dateEntreePrevue: '',
      regleDePaye: '',
      regleDePayeCommentaire: '',
    },
    access: {
      systemes: [],
      posHebergement: [],
      badgeZones: '',
      codeAlarmeDetails: '',
      justification: '',
      stationnement: '',
    },
    equipment: {
      equipements: [],
      notes: '',
    },
    applications: {
      applications: [],
      autreLogiciel: '',
    },
    offboarding: {
      employees: [],
      derniereJournee: '',
      indemniteVacances: '',
      raisonArret: '',
      detailsRaison: '',
      reembaucheriez: '',
      attachments: [],
      commentairesRH: '',
      commentairesIT: '',
      commentairesParkingAcces: '',
      commentairesRedingote: '',
    },
  };
}
