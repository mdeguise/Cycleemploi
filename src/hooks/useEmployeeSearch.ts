import { useQuery } from '@tanstack/react-query';
import { useApi } from '../api/ApiContext';
import { useDebouncedValue } from './useDebouncedValue';

/** Live search against /api/employees/search (WorkdayDemographic), debounced. Shared by every
 * employee picker in the app — the single-select onboarding/réactivation flow, the offboarding
 * multi-select, and the batch onboarding grid. Réactivation (and batch mode, which can mix in
 * réactivations) must include Terminated employees; a plain onboarding search must not. */
export function useEmployeeSearch(query: string, includeTerminated: boolean) {
  const api = useApi();
  const debounced = useDebouncedValue(query.trim(), 300);
  return useQuery({
    queryKey: ['employees', 'search', debounced, includeTerminated],
    queryFn: () => api.employees.search(debounced, includeTerminated),
    enabled: debounced.length >= 2,
  });
}
