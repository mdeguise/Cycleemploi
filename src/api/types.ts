// Hand-written to match backend/Api/Models/Dtos/*.cs exactly. The plan recommends generating this
// from the API's OpenAPI spec (via NSwag or openapi-typescript) once the backend is stable enough
// to have one — doing that keeps this file from drifting out of sync by hand. Not done yet in this
// pass; worth doing before this ships.

export interface EmployeeDto {
  employeeId: string;
  prenom: string;
  nom: string;
  poste?: string | null;
  departement?: string | null;
  codeEmploi?: string | null;
  typeEmploi?: string | null;
  gestionnaire?: string | null;
}

export interface AccessSystemDto {
  nom: string;
  description: string;
}

export interface EquipmentItemDto {
  nom: string;
  categorie: string;
}

export interface ApplicationItemDto {
  nom: string;
  editeur: string;
}

export interface CatalogsDto {
  departements: string[];
  typesEmploi: string[];
  reglesDePaye: string[];
  regleDePayeAutre: string;
  systemesAcces: AccessSystemDto[];
  posHebergementSystemes: string[];
  equipements: EquipmentItemDto[];
  applications: ApplicationItemDto[];
  ouiNon: string[];
  raisonsArret: string[];
  reembaucheriezOptions: string[];
}

export interface MeDto {
  objectId: string;
  displayName: string;
  email?: string | null;
  isHr: boolean;
}

export type RequestTypeApi = 'Onboarding' | 'Reactivation' | 'Offboarding';
export type RequestStatusApi = 'Brouillon' | 'Soumise' | 'EnTraitement' | 'Completee';

export interface RequestEmployeeDto {
  workdayEmployeeId: string;
  nameSnapshot: string;
  positionSnapshot?: string | null;
  departementSnapshot?: string | null;
  codeEmploiSnapshot?: string | null;
  typeEmploiSnapshot?: string | null;
  gestionnaireSnapshot?: string | null;
}

export interface RequestDto {
  requestId: number;
  requestNumber: string;
  requestType: RequestTypeApi;
  status: RequestStatusApi;
  demandePar: string;
  createdAt: string;
  employees: RequestEmployeeDto[];
  dateEntreePrevue?: string | null;
  regleDePaye?: string | null;
  regleDePayeCommentaire?: string | null;
  systemesAcces: string[];
  badgeZones?: string | null;
  codeAlarmeDetails?: string | null;
  systemePosHebergement: string[];
  stationnementRequis?: string | null;
  justificationAcces?: string | null;
  equipements: string[];
  notesEquipement?: string | null;
  applications: string[];
  autreLogicielRequis?: string | null;
  derniereJournee?: string | null;
  indemniteVacances?: string | null;
  raisonArret?: string | null;
  detailsRaison?: string | null;
  reembaucheriez?: string | null;
  commentairesIT?: string | null;
  commentairesStationnement?: string | null;
  commentairesPuceAcces?: string | null;
  commentairesRedingote?: string | null;
  /** Omitted from the JSON entirely (not just null) when the caller isn't authorized to read it —
   * see backend/Api/Controllers/RequestsController.cs. */
  commentairesRH?: string | null;
}

export type UpdateRequestDto = Omit<
  RequestDto,
  'requestId' | 'requestNumber' | 'requestType' | 'status' | 'demandePar' | 'createdAt'
>;

export interface CreateRequestDto {
  requestType: RequestTypeApi;
}

export interface D365SecurityRoleMappingDto {
  id: number;
  jobCode: string;
  role: string;
  positionTitle?: string | null;
}

export interface CreateD365SecurityRoleMappingDto {
  jobCode: string;
  role: string;
}

export interface D365UserSecurityRoleDto {
  id: number;
  userName: string;
  securityRole: string;
  employeeId?: string | null;
  jobCode?: string | null;
  positionTitle?: string | null;
}

// --- Reconciliation / "Écarts" (DiscrepanciesController) ---

export interface DiscrepancySummaryDto {
  generatedUtc: string;
  dynawayLicensesTotal: number;
  tremblantDynawayCount: number;
  noActiveAdCount: number;
  dynawayNoD365RoleCount: number;
  d365InactiveWorkdayCount: number;
}

export interface TremblantDynawayRowDto {
  name: string | null;
  login: string | null;
  adEnabled: boolean;
  hasD365Role: boolean;
  d365RoleCount: number;
}

export interface NoActiveAdRowDto {
  source: string;
  name: string;
  login: string | null;
  status: string;
}

export interface DynawayNoRoleRowDto {
  name: string | null;
  login: string | null;
  adEnabled: boolean;
}

export interface D365InactiveWorkdayRowDto {
  userName: string;
  employeeId: string | null;
  workdayStatus: string;
  d365RoleCount: number;
  roles: string;
}

export interface DiscrepanciesDto {
  summary: DiscrepancySummaryDto;
  tremblantDynaway: TremblantDynawayRowDto[];
  noActiveAd: NoActiveAdRowDto[];
  dynawayNoD365Role: DynawayNoRoleRowDto[];
  d365InactiveWorkday: D365InactiveWorkdayRowDto[];
}
