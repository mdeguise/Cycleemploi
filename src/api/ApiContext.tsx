import { createContext, useContext, useMemo, type ReactNode } from 'react';
import { useMsal } from '@azure/msal-react';
import { createApiClient } from './client';
import { createApi, type Api } from './endpoints';

const ApiContext = createContext<Api | undefined>(undefined);

export function ApiProvider({ children }: { children: ReactNode }) {
  const { instance } = useMsal();

  // Recreated only when the MSAL instance identity changes (effectively never, in practice) —
  // the client itself reads the active account fresh on every call, so it doesn't need to depend
  // on sign-in state directly.
  const api = useMemo(() => createApi(createApiClient(instance)), [instance]);

  return <ApiContext.Provider value={api}>{children}</ApiContext.Provider>;
}

export function useApi() {
  const ctx = useContext(ApiContext);
  if (!ctx) throw new Error('useApi must be used within ApiProvider');
  return ctx;
}
