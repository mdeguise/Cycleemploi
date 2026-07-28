import type { ApiClient } from './client';
import type {
  CatalogsDto,
  CreateRequestDto,
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
  };
}

export type Api = ReturnType<typeof createApi>;
