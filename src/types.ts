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
  /** UI-only signal captured from the employee search result at selection time — not persisted
   * (RequestEmployee has no PayGroup column; the backend re-derives it live from Workday at
   * submit time for validation — see RequestsController.ValidateForSubmitAsync). Drives whether
   * "Règle de paye" is required/grayed-out for CAN Tremblant-Non Union employees. */
  employeePayGroup: string;
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

/** "Commentaires et suivis" for Onboarding/Réactivation — mirrors OffboardingInfo's per-department
 * comment fields (see Step3DepartmentComments.tsx / Step6DepartmentComments.tsx). commentairesRH is
 * confidentiality-gated the same way as offboarding's — see RequestAuthorizationService. */
export interface CommentsInfo {
  commentairesRH: string;
  commentairesIT: string;
  commentairesStationnement: string;
  commentairesPuceAcces: string;
  commentairesRedingote: string;
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
  commentairesStationnement: string;
  commentairesPuceAcces: string;
  commentairesRedingote: string;
  dateRetourConnue: string;
  dateRetourTravail: string;
  preavisRecu: string;
  motifNonAdmissibilite: string;
}

export interface OnboardingRequest {
  /** No partial-save state — everything is local/optimistic until the single submitRequest() call
   * at the very end (see WizardContext). requestId/demandeNumero are only ever populated from that
   * call's response, after the wizard is done. */
  requestId: number | null;
  demandeNumero: string;
  dateCreation: string;
  demandePar: string;
  typeDemande: TypeDemande;
  employee: EmployeeSelectionInfo;
  access: AccessInfo;
  equipment: EquipmentInfo;
  applications: ApplicationsInfo;
  comments: CommentsInfo;
  offboarding: OffboardingInfo;
}

export function createEmptyRequest(demandePar: string): OnboardingRequest {
  return {
    requestId: null,
    demandeNumero: '',
    dateCreation: new Date().toISOString().slice(0, 10),
    demandePar,
    typeDemande: '',
    employee: {
      employee: null,
      dateEntreePrevue: '',
      regleDePaye: '',
      regleDePayeCommentaire: '',
      employeePayGroup: '',
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
    comments: {
      commentairesRH: '',
      commentairesIT: '',
      commentairesStationnement: '',
      commentairesPuceAcces: '',
      commentairesRedingote: '',
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
      commentairesStationnement: '',
      commentairesPuceAcces: '',
      commentairesRedingote: '',
      dateRetourConnue: '',
      dateRetourTravail: '',
      preavisRecu: '',
      motifNonAdmissibilite: '',
    },
  };
}
