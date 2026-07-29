import { createContext, useContext, useMemo, type ReactNode } from 'react';
import { createApiClient } from './client';
import { createApi, type Api } from './endpoints';

const ApiContext = createContext<Api | undefined>(undefined);

export function ApiProvider({ children }: { children: ReactNode }) {
  const api = useMemo(() => createApi(createApiClient()), []);

  return <ApiContext.Provider value={api}>{children}</ApiContext.Provider>;
}

export function useApi() {
  const ctx = useContext(ApiContext);
  if (!ctx) throw new Error('useApi must be used within ApiProvider');
  return ctx;
}
