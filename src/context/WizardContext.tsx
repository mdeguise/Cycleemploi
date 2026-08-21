import { createContext, useContext, useMemo, useRef, useState, type ReactNode } from 'react';
import { createEmptyRequest, TYPE_DEMANDE_TERMINAISON, type OnboardingRequest, type TypeDemande } from '../types';
import { REGLE_DE_PAYE_AUTRE, PAY_GROUP_NON_UNION } from '../data/catalogs';
import { useApi } from '../api/ApiContext';
import type { RequestTypeApi, UpdateRequestDto } from '../api/types';

export interface StepDescriptor {
  key: string;
  numero: number;
  titre: string;
  sousTitre: string;
}

export const ONBOARDING_STEPS: StepDescriptor[] = [
  { key: 'employee', numero: 1, titre: 'Employé', sousTitre: "Sélection de l'employé" },
  { key: 'position', numero: 2, titre: 'Poste et département', sousTitre: 'Détails du poste' },
  { key: 'access', numero: 3, titre: 'Accès et comptes', sousTitre: 'Systèmes et accès requis' },
  { key: 'equipment', numero: 4, titre: 'Équipement', sousTitre: 'Matériel requis' },
  { key: 'applications', numero: 5, titre: 'Applications', sousTitre: 'Logiciels et licences' },
  { key: 'comments', numero: 6, titre: 'Commentaires et suivis', sousTitre: 'RH, TI, stationnement, matériel' },
  { key: 'review', numero: 7, titre: 'Révision et soumission', sousTitre: 'Vérifier et envoyer' },
];

export const OFFBOARDING_STEPS: StepDescriptor[] = [
  { key: 'employees', numero: 1, titre: 'Employé(s)', sousTitre: 'Sélection des employés' },
  { key: 'cessation', numero: 2, titre: 'Détails de la cessation', sousTitre: 'Informations requises' },
  { key: 'comments', numero: 3, titre: 'Commentaires et suivis', sousTitre: 'RH, TI, stationnement, matériel' },
  { key: 'review', numero: 4, titre: 'Révision et soumission', sousTitre: 'Vérifier et envoyer' },
];

function stepsFor(typeDemande: TypeDemande): StepDescriptor[] {
  return typeDemande === TYPE_DEMANDE_TERMINAISON ? OFFBOARDING_STEPS : ONBOARDING_STEPS;
}

function toRequestTypeApi(typeDemande: TypeDemande): RequestTypeApi {
  if (typeDemande === TYPE_DEMANDE_TERMINAISON) return 'Offboarding';
  if (typeDemande === 'Réactivation') return 'Reactivation';
  return 'Onboarding';
}

/** Maps local wizard state to the shape PUT /api/requests/{id} expects. Pure function, no
 * side effects — kept next to the context that's the only caller for now, move to src/api if a
 * second caller ever needs it. */
function toUpdateDto(request: OnboardingRequest): UpdateRequestDto {
  const isOffboarding = request.typeDemande === TYPE_DEMANDE_TERMINAISON;
  return {
    employees: isOffboarding
      ? request.offboarding.employees.map((e) => ({
          workdayEmployeeId: e.workdayEmployeeId,
          nameSnapshot: `${e.prenom} ${e.nom}`,
          positionSnapshot: e.poste,
          departementSnapshot: e.departement,
          codeEmploiSnapshot: e.codeEmploi,
          typeEmploiSnapshot: e.typeEmploi,
          gestionnaireSnapshot: e.gestionnaire,
        }))
      : request.employee.employee
        ? [
            {
              workdayEmployeeId: request.employee.employee.workdayEmployeeId,
              nameSnapshot: `${request.employee.employee.prenom} ${request.employee.employee.nom}`,
              positionSnapshot: request.employee.employee.poste,
              departementSnapshot: request.employee.employee.departement,
              codeEmploiSnapshot: request.employee.employee.codeEmploi,
              typeEmploiSnapshot: request.employee.employee.typeEmploi,
              gestionnaireSnapshot: request.employee.employee.gestionnaire,
            },
          ]
        : [],
    dateEntreePrevue: request.employee.dateEntreePrevue || null,
    regleDePaye: request.employee.regleDePaye || null,
    regleDePayeCommentaire: request.employee.regleDePayeCommentaire || null,
    systemesAcces: request.access.systemes,
    badgeZones: request.access.badgeZones || null,
    codeAlarmeDetails: request.access.codeAlarmeDetails || null,
    systemePosHebergement: request.access.posHebergement,
    stationnementRequis: request.access.stationnement || null,
    justificationAcces: request.access.justification || null,
    equipements: request.equipment.equipements,
    notesEquipement: request.equipment.notes || null,
    applications: request.applications.applications,
    autreLogicielRequis: request.applications.autreLogiciel || null,
    commentairesRH: isOffboarding
      ? request.offboarding.commentairesRH || null
      : request.comments.commentairesRH || null,
    commentairesIT: isOffboarding ? request.offboarding.commentairesIT || null : request.comments.commentairesIT || null,
    commentairesStationnement: isOffboarding
      ? request.offboarding.commentairesStationnement || null
      : request.comments.commentairesStationnement || null,
    commentairesPuceAcces: isOffboarding
      ? request.offboarding.commentairesPuceAcces || null
      : request.comments.commentairesPuceAcces || null,
    commentairesRedingote: isOffboarding
      ? request.offboarding.commentairesRedingote || null
      : request.comments.commentairesRedingote || null,
    derniereJournee: request.offboarding.derniereJournee || null,
    indemniteVacances: request.offboarding.indemniteVacances || null,
    raisonArret: request.offboarding.raisonArret || null,
    detailsRaison: request.offboarding.detailsRaison || null,
    reembaucheriez: request.offboarding.reembaucheriez || null,
    dateRetourConnue: request.offboarding.dateRetourConnue || null,
    dateRetourTravail: request.offboarding.dateRetourTravail || null,
    preavisRecu: request.offboarding.preavisRecu || null,
    motifNonAdmissibilite: request.offboarding.motifNonAdmissibilite || null,
  };
}

interface WizardContextValue {
  request: OnboardingRequest;
  setRequest: React.Dispatch<React.SetStateAction<OnboardingRequest>>;
  currentStep: number;
  setCurrentStep: (step: number) => void;
  furthestStep: number;
  goNext: () => Promise<void>;
  goBack: () => void;
  goToStep: (step: number) => void;
  isStepValid: (step: number) => boolean;
  progressLabel: string;
  steps: StepDescriptor[];
  stepCount: number;
  setTypeDemande: (typeDemande: TypeDemande) => Promise<void>;
  /** Persists current local state to the server — called on every step navigation (a natural
   * checkpoint) and by the "Enregistrer le brouillon" button. No-ops until a request exists
   * (i.e. before a type is selected). */
  syncToServer: () => Promise<void>;
  isSyncing: boolean;
  submitRequest: () => Promise<void>;
}

const WizardContext = createContext<WizardContextValue | undefined>(undefined);

function validateStep(step: number, request: OnboardingRequest): boolean {
  if (request.typeDemande === TYPE_DEMANDE_TERMINAISON) {
    switch (step) {
      case 0:
        return Boolean(request.typeDemande && request.offboarding.employees.length > 0);
      case 1: {
        const o = request.offboarding;
        return Boolean(o.derniereJournee && o.indemniteVacances && o.raisonArret && o.detailsRaison && o.reembaucheriez);
      }
      default:
        return true;
    }
  }

  switch (step) {
    case 0: {
      const e = request.employee;
      const regleDePayeNonRequise = e.employeePayGroup === PAY_GROUP_NON_UNION;
      const regleValid =
        regleDePayeNonRequise ||
        (e.regleDePaye && (e.regleDePaye !== REGLE_DE_PAYE_AUTRE || Boolean(e.regleDePayeCommentaire)));
      return Boolean(request.typeDemande && e.employee && e.dateEntreePrevue && regleValid);
    }
    default:
      return true;
  }
}

export function WizardProvider({ children, demandePar }: { children: ReactNode; demandePar: string }) {
  const api = useApi();
  const [request, setRequest] = useState<OnboardingRequest>(() => createEmptyRequest(demandePar));
  const [currentStep, setCurrentStep] = useState(0);
  const [furthestStep, setFurthestStep] = useState(0);
  const [isSyncing, setIsSyncing] = useState(false);
  // Guards against a stale in-flight create() firing twice if the user double-clicks a type card.
  const creatingRef = useRef(false);

  const steps = useMemo(() => stepsFor(request.typeDemande), [request.typeDemande]);
  const stepCount = steps.length;

  const goToStep = (step: number) => {
    const clamped = Math.max(0, Math.min(stepCount - 1, step));
    setCurrentStep(clamped);
    setFurthestStep((f) => Math.max(f, clamped));
  };

  const syncToServer = async () => {
    if (!request.requestId) return;
    setIsSyncing(true);
    try {
      await api.requests.update(request.requestId, toUpdateDto(request));
    } finally {
      setIsSyncing(false);
    }
  };

  const goNext = async () => {
    await syncToServer();
    goToStep(currentStep + 1);
  };
  const goBack = () => goToStep(currentStep - 1);

  const isStepValid = (step: number) => validateStep(step, request);

  const progressLabel = useMemo(() => `${currentStep + 1} / ${stepCount} étapes`, [currentStep, stepCount]);

  const setTypeDemande = async (typeDemande: TypeDemande) => {
    setFurthestStep(currentStep);

    if (!request.requestId && typeDemande && !creatingRef.current) {
      creatingRef.current = true;
      try {
        const created = await api.requests.create({ requestType: toRequestTypeApi(typeDemande) });
        setRequest((prev) => ({
          ...prev,
          typeDemande,
          requestId: created.requestId,
          demandeNumero: created.requestNumber,
          dateCreation: created.createdAt.slice(0, 10),
        }));
      } finally {
        creatingRef.current = false;
      }
      return;
    }

    setRequest((prev) => ({ ...prev, typeDemande }));
  };

  const submitRequest = async () => {
    if (!request.requestId) throw new Error('No request to submit — this should be unreachable.');
    await syncToServer();
    await api.requests.submit(request.requestId);
    setRequest((prev) => ({ ...prev, statut: 'Soumise' }));
  };

  const value: WizardContextValue = {
    request,
    setRequest,
    currentStep,
    setCurrentStep,
    furthestStep,
    goNext,
    goBack,
    goToStep,
    isStepValid,
    progressLabel,
    steps,
    stepCount,
    setTypeDemande,
    syncToServer,
    isSyncing,
    submitRequest,
  };

  return <WizardContext.Provider value={value}>{children}</WizardContext.Provider>;
}

export function useWizard() {
  const ctx = useContext(WizardContext);
  if (!ctx) throw new Error('useWizard must be used within WizardProvider');
  return ctx;
}
