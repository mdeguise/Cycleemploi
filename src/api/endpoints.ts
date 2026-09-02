import type { ApiClient } from './client';
import type {
  AdAccountDto,
  AdminRequestDetailDto,
  AdminRequestListDto,
  TicketViewDto,
  AppUserDto,
  AppUserRole,
  CatalogsDto,
  CreateAppUserDto,
  RetryTicketResultDto,
  CreateD365SecurityRoleMappingDto,
  CreateHelpTicketDto,
  CreateRequestDto,
  D365SecurityRoleMappingDto,
  D365UserSecurityRoleDto,
  DiscrepanciesDto,
  EmployeeDto,
  HelpTicketResultDto,
  HelpUrlDto,
  MeDto,
  RequestDto,
  TicketTemplateDto,
  UpdateRequestDto,
  UpdateTicketTemplateDto,
  D365ApproverDto,
  CreateD365ApproverDto,
  D365AccessApprovalSummaryDto,
  D365AccessApprovalDetailDto,
  CompleteD365AccessApprovalDto,
  CompleteD365AccessApprovalResultDto,
} from './types';

export function createApi(client: ApiClient) {
  return {
    auth: {
      me: () => client.get<MeDto>('/api/auth/me'),
      helpUrl: () => client.get<HelpUrlDto>('/api/auth/help-url'),
      createHelpTicket: (dto: CreateHelpTicketDto) =>
        client.post<HelpTicketResultDto>('/api/auth/help-ticket', dto),
    },
    catalogs: {
      get: () => client.get<CatalogsDto>('/api/catalogs'),
    },
    employees: {
      search: (q: string) =>
        client.get<EmployeeDto[]>(`/api/employees/search?q=${encodeURIComponent(q)}`),
      getById: (workdayId: number) => client.get<EmployeeDto>(`/api/employees/${workdayId}`),
    },
    requests: {
      create: (dto: CreateRequestDto) => client.post<RequestDto>('/api/requests', dto),
      get: (id: number) => client.get<RequestDto>(`/api/requests/${id}`),
      update: (id: number, dto: UpdateRequestDto) =>
        client.put<void>(`/api/requests/${id}`, dto),
      submit: (id: number) => client.post<void>(`/api/requests/${id}/submit`),
    },
    d365SecurityRoles: {
      list: () => client.get<D365SecurityRoleMappingDto[]>('/api/d365-security-roles'),
      catalog: () => client.get<string[]>('/api/d365-security-roles/catalog'),
      create: (dto: CreateD365SecurityRoleMappingDto) =>
        client.post<D365SecurityRoleMappingDto>('/api/d365-security-roles', dto),
      remove: (id: number) => client.delete<void>(`/api/d365-security-roles/${id}`),
    },
    d365UserSecurityRoles: {
      list: (unmatchedOnly: boolean) =>
        client.get<D365UserSecurityRoleDto[]>(`/api/d365-user-security-roles?unmatchedOnly=${unmatchedOnly}`),
      link: (id: number, employeeId: string) =>
        client.put<D365UserSecurityRoleDto>(`/api/d365-user-security-roles/${id}/link`, { employeeId }),
      remove: (id: number) => client.delete<void>(`/api/d365-user-security-roles/${id}`),
    },
    discrepancies: {
      get: () => client.get<DiscrepanciesDto>('/api/discrepancies'),
    },
    d365Approvers: {
      list: () => client.get<D365ApproverDto[]>('/api/d365-approvers'),
      adSearch: (q: string) =>
        client.get<AdAccountDto[]>(`/api/d365-approvers/ad-search?q=${encodeURIComponent(q)}`),
      add: (dto: CreateD365ApproverDto) => client.post<D365ApproverDto>('/api/d365-approvers', dto),
      remove: (id: number) => client.delete<void>(`/api/d365-approvers/${id}`),
    },
    d365AccessApprovals: {
      list: () => client.get<D365AccessApprovalSummaryDto[]>('/api/d365-access-approvals'),
      detail: (requestId: number) =>
        client.get<D365AccessApprovalDetailDto>(`/api/d365-access-approvals/${requestId}`),
      complete: (requestId: number, dto: CompleteD365AccessApprovalDto) =>
        client.post<CompleteD365AccessApprovalResultDto>(`/api/d365-access-approvals/${requestId}/complete`, dto),
    },
    ticketTemplates: {
      list: () => client.get<TicketTemplateDto[]>('/api/ticket-templates'),
      update: (key: string, dto: UpdateTicketTemplateDto) =>
        client.put<TicketTemplateDto>(`/api/ticket-templates/${encodeURIComponent(key)}`, dto),
    },
    appUsers: {
      list: () => client.get<AppUserDto[]>('/api/app-users'),
      add: (dto: CreateAppUserDto) => client.post<AppUserDto>('/api/app-users', dto),
      remove: (id: number) => client.delete<void>(`/api/app-users/${id}`),
      updateRole: (id: number, role: AppUserRole) =>
        client.put<AppUserDto>(`/api/app-users/${id}/role`, { role }),
      adSearch: (q: string) =>
        client.get<AdAccountDto[]>(`/api/app-users/ad-search?q=${encodeURIComponent(q)}`),
    },

    adminRequests: {
      list: (params: {
        q?: string;
        status?: string;
        requestType?: string;
        onlyFailures?: boolean;
        page?: number;
        pageSize?: number;
      }) => {
        const qs = new URLSearchParams();
        if (params.q) qs.set('q', params.q);
        if (params.status) qs.set('status', params.status);
        if (params.requestType) qs.set('requestType', params.requestType);
        if (params.onlyFailures) qs.set('onlyFailures', 'true');
        if (params.page) qs.set('page', String(params.page));
        if (params.pageSize) qs.set('pageSize', String(params.pageSize));
        return client.get<AdminRequestListDto>(`/api/admin/requests?${qs.toString()}`);
      },
      detail: (id: number) => client.get<AdminRequestDetailDto>(`/api/admin/requests/${id}`),
      ticketView: (params: {
        q?: string;
        status?: string;
        requestType?: string;
        onlyFailures?: boolean;
        page?: number;
        pageSize?: number;
      }) => {
        const qs = new URLSearchParams();
        if (params.q) qs.set('q', params.q);
        if (params.status) qs.set('status', params.status);
        if (params.requestType) qs.set('requestType', params.requestType);
        if (params.onlyFailures) qs.set('onlyFailures', 'true');
        if (params.page) qs.set('page', String(params.page));
        if (params.pageSize) qs.set('pageSize', String(params.pageSize));
        return client.get<TicketViewDto>(`/api/admin/requests/ticket-view?${qs.toString()}`);
      },
      retryTicket: (requestId: number, ticketId: number) =>
        client.post<RetryTicketResultDto>(`/api/admin/requests/${requestId}/tickets/${ticketId}/retry`, {}),
    },
  };
}

export type Api = ReturnType<typeof createApi>;
