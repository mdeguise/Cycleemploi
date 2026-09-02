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
  payGroup?: string | null;
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
  /** 'Admin' | 'Lecteur' | null when the user has no Administration access at all. */
  adminRole?: AppUserRole | null;
  /** Full Administration rights: retry tickets, edit templates, manage app users. */
  isAppAdmin: boolean;
  /** @deprecated alias for isAppAdmin, kept while the UI migrates. */
  isTicketTemplateAdmin: boolean;
  /** ANY D365Approver row (global or scoped to a Position Title) — a DIFFERENT table from AppUsers. */
  isD365Approver: boolean;
  /** "IT Personnel" — sees the D365 tracking list and status, never the Envoyer action. */
  isD365Viewer: boolean;
}

export interface HelpUrlDto {
  url: string;
}

export interface CreateHelpTicketDto {
  description: string;
}

export interface HelpTicketResultDto {
  ticketId: number;
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
  dateRetourConnue?: string | null;
  dateRetourTravail?: string | null;
  preavisRecu?: string | null;
  motifNonAdmissibilite?: string | null;
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

// --- D365 access approval (D365ApproversController / D365AccessApprovalsController) ---

export interface D365ApproverDto {
  d365ApproverId: number;
  sam: string;
  displayName: string;
  email?: string | null;
  positionTitle?: string | null;
  createdAt: string;
  createdByDisplayName?: string | null;
}

export interface CreateD365ApproverDto {
  sam: string;
  displayName: string;
  email?: string | null;
  positionTitle?: string | null;
}

/** One row of the "Titres de poste" master list. jobCodes is informational only — approver
 * routing matches on positionTitle alone, never jobCode. */
export interface D365PositionTitleDto {
  positionTitle: string;
  jobCodes: string[];
}

export interface D365ViewerDto {
  d365ViewerId: number;
  sam: string;
  displayName: string;
  email?: string | null;
  createdAt: string;
  createdByDisplayName?: string | null;
}

export interface CreateD365ViewerDto {
  sam: string;
  displayName: string;
  email?: string | null;
}

export interface D365PeerRoleDto {
  employeeName: string;
  employeeId: string;
  roles: string[];
}

export interface D365AccessApprovalSummaryDto {
  requestId: number;
  requestNumber: string;
  employeeName: string;
  positionTitle?: string | null;
  managerName?: string | null;
  requesterName: string;
  startDate?: string | null;
  status: string;
  createdAt: string;
  completedAt?: string | null;
  completedByDisplayName?: string | null;
  cancelledAt?: string | null;
  cancelledByDisplayName?: string | null;
  cancelReason?: string | null;
  ticketNumber?: string | null;
  ticketState?: string | null;
  ticketStateLabel?: string | null;
}

export interface D365AccessApprovalDetailDto {
  requestId: number;
  requestNumber: string;
  status: string;
  cancelledByDisplayName?: string | null;
  cancelledAt?: string | null;
  cancelReason?: string | null;
  canComplete: boolean;
  canCancel: boolean;
  requesterName: string;
  employeeName: string;
  employeeEmail?: string | null;
  managerName?: string | null;
  positionTitle?: string | null;
  jobCode?: string | null;
  departement?: string | null;
  startDate?: string | null;
  jobTitleEnglish?: string | null;
  /** Fixed at "6201" for every request — display only, never an editable input. */
  legalEntity: string;
  /** The employee's Workday Cost_Center, verbatim — display only, never an editable input. */
  departmentNumber?: string | null;
  /** "New Access" | "Change Access" | "Remove Access" — display only, set at submission (by the
   * D365AccessRequest app, or "New Access" by default for the onboarding-wizard-driven flow). */
  accessType: string;
  approvalLimit?: number | null;
  apAccessDetails?: string | null;
  additionalLegalEntities?: string | null;
  defaultShippingAddress?: string | null;
  comments?: string | null;
  levyEmployee?: boolean | null;
  roles: string[];
  roleCatalog: string[];
  peers: D365PeerRoleDto[];
}

export interface CompleteD365AccessApprovalDto {
  jobTitleEnglish: string;
  approvalLimit: number;
  levyEmployee: boolean;
  apAccessDetails?: string | null;
  additionalLegalEntities?: string | null;
  defaultShippingAddress?: string | null;
  comments?: string | null;
  roles: string[];
}

export interface CompleteD365AccessApprovalResultDto {
  succeeded: boolean;
  ticketNumber?: string | null;
  error?: string | null;
}

export interface CancelD365AccessApprovalDto {
  reason?: string | null;
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

// --- Ticket templates (admin) ---
// Content/defaultContent are JSON strings the frontend parses/builds itself, matching
// backend/Api/Models/Entities/TicketTemplateContent.cs (InlineTemplateContent / BlockTemplateContent,
// per `shape`) — the admin never sees this JSON or any {{placeholder}} syntax directly.

export interface TicketTemplateFieldDto {
  key: string;
  label: string;
}

export type TicketTemplateShape = 'Inline' | 'Block';

export interface TicketTemplateDto {
  key: string;
  label: string;
  description: string;
  shape: TicketTemplateShape;
  content: string;
  defaultContent: string;
  requestFields: TicketTemplateFieldDto[];
  employeeFields: TicketTemplateFieldDto[];
  updatedAt?: string | null;
  updatedByDisplayName?: string | null;
}

export interface UpdateTicketTemplateDto {
  content: string;
}

export interface InlinePart {
  type: 'field' | 'text';
  fieldKey?: string;
  text?: string;
}

export interface InlineTemplateContent {
  parts: InlinePart[];
}

export interface EmployeeFieldLine {
  label: string;
  fieldKey: string;
}

export interface TemplateBlock {
  type: 'heading' | 'field' | 'employeeGroup';
  headingText?: string | null;
  label?: string | null;
  fieldKey?: string | null;
  employeeGroupHeading?: string | null;
  employeeFields: EmployeeFieldLine[];
}

export interface BlockTemplateContent {
  blocks: TemplateBlock[];
}

// --- App users / Ticket Template admins ---

export type AppUserRole = 'Admin' | 'Lecteur';

export interface AppUserDto {
  appUserId: number;
  /** Bare sAMAccountName — the authorization key (domain-agnostic, survives the ENTERPRISE migration). */
  sam: string;
  displayName: string;
  /** Informational only; null for admin (*_adm) accounts, which have no AD mail attribute. */
  email?: string | null;
  role: AppUserRole;
  createdAt: string;
  createdByDisplayName?: string | null;
}

export interface CreateAppUserDto {
  sam: string;
  displayName: string;
  email?: string | null;
  role: AppUserRole;
}

/** One hit from the AD people search behind the "add a user" picker. */
export interface AdAccountDto {
  sam: string;
  displayName: string;
  email?: string | null;
}

/** Which downstream system a ticket row represents. */
export type TicketKind =
  | 'Freshdesk'
  | 'FreshdeskChildWithJobCodes'
  | 'FreshdeskChildWithoutJobCodes'
  | 'Tdx'
  | 'D365Badge'
  | 'D365Access';

export type TicketOutcome = 'Created' | 'Failed';

export interface RequestTicketDto {
  requestTicketId: number;
  kind: TicketKind;
  /** Human-readable French label, supplied by the API so every surface agrees on wording. */
  kindLabel: string;
  outcome: TicketOutcome;
  /** Null for request-level tickets (the Freshdesk parent and its children). */
  requestEmployeeId?: number | null;
  employeeName?: string | null;
  ticketNumber?: string | null;
  errorType?: string | null;
  errorMessage?: string | null;
  attemptCount: number;
  firstAttemptAt: string;
  lastAttemptAt: string;
}

export interface RetryTicketResultDto {
  succeeded: boolean;
  ticketNumber?: string | null;
  error?: string | null;
}

export interface AdminRequestSummaryDto {
  requestId: number;
  requestNumber: string;
  requestType: RequestTypeApi;
  status: RequestStatusApi;
  demandePar: string;
  createdAt: string;
  submittedAt?: string | null;
  employeeNames: string[];
  ticketsCreated: number;
  ticketsFailed: number;
}

export interface AdminRequestListDto {
  total: number;
  page: number;
  pageSize: number;
  items: AdminRequestSummaryDto[];
}

export interface AdminRequestDetailDto {
  request: RequestDto;
  requesterEmail?: string | null;
  submittedAt?: string | null;
  /** False for a Lecteur; the API refuses the retry regardless. */
  canRetry: boolean;
  tickets: RequestTicketDto[];
}

/** 'Open' | 'Closed' | 'Unknown'. Unknown means the source system could not be reached — kept
 *  distinct from Closed on purpose, since reporting an unreachable ticket as closed is the kind of
 *  wrong answer someone acts on. */
export type LiveTicketState = 'Open' | 'Closed' | 'Unknown';

export interface TicketRefDto {
  requestTicketId: number;
  ticketNumber: string;
  state: LiveTicketState;
  /** The source system's own wording ("En attente", "In Process"), when it gave one. */
  stateLabel?: string | null;
  employeeName?: string | null;
}

export interface TicketViewRowDto {
  requestId: number;
  requestNumber: string;
  requestType: RequestTypeApi;
  status: RequestStatusApi;
  submittedAt?: string | null;
  employeeNames: string[];
  freshdesk: TicketRefDto[];
  tdx: TicketRefDto[];
  failedCount: number;
}

export interface TicketViewDto {
  total: number;
  page: number;
  pageSize: number;
  items: TicketViewRowDto[];
  /** True when at least one live-status lookup failed, so the UI can say so rather than show blanks. */
  hasUnknownStatuses: boolean;
}
