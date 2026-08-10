import type { ApiClient } from './client';
import type {
  CatalogsDto,
  CreateD365SecurityRoleMappingDto,
  CreateHelpTicketDto,
  CreateRequestDto,
  D365JobCodeTemplateDto,
  D365JobCodeTemplateSummaryDto,
  D365SecurityRoleMappingDto,
  D365UserSecurityRoleDto,
  DiscrepanciesDto,
  EmployeeDto,
  HelpTicketResultDto,
  HelpUrlDto,
  MeDto,
  RequestDto,
  UpdateRequestDto,
  UpsertD365JobCodeTemplateDto,
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
    d365JobCodeTemplates: {
      list: () => client.get<D365JobCodeTemplateSummaryDto[]>('/api/d365-jobcode-templates'),
      catalog: () => client.get<string[]>('/api/d365-jobcode-templates/catalog'),
      get: (jobCode: string) =>
        client.get<D365JobCodeTemplateDto>(`/api/d365-jobcode-templates/${encodeURIComponent(jobCode)}`),
      upsert: (jobCode: string, dto: UpsertD365JobCodeTemplateDto) =>
        client.put<D365JobCodeTemplateDto>(`/api/d365-jobcode-templates/${encodeURIComponent(jobCode)}`, dto),
      remove: (jobCode: string) => client.delete<void>(`/api/d365-jobcode-templates/${encodeURIComponent(jobCode)}`),
    },
  };
}

export type Api = ReturnType<typeof createApi>;
