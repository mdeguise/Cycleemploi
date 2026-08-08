import type { ApiClient } from './client';
import type {
  CatalogsDto,
  CreateD365SecurityRoleMappingDto,
  CreateRequestDto,
  D365SecurityRoleMappingDto,
  EmployeeDto,
  MeDto,
  RequestDto,
  UpdateRequestDto,
} from './types';

export function createApi(client: ApiClient) {
  return {
    auth: {
      me: () => client.get<MeDto>('/api/auth/me'),
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
      create: (dto: CreateD365SecurityRoleMappingDto) =>
        client.post<D365SecurityRoleMappingDto>('/api/d365-security-roles', dto),
      remove: (id: number) => client.delete<void>(`/api/d365-security-roles/${id}`),
    },
  };
}

export type Api = ReturnType<typeof createApi>;
