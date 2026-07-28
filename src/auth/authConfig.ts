import type { Configuration } from '@azure/msal-browser';

// TODO Phase 0: replace with the real SPA app registration's values once created by someone with
// tenant admin rights (see backend/Api/appsettings.json's matching TODO for the API app
// registration, and the plan's Phase 0 checklist). VITE_ prefix makes these build-time env vars
// available to the browser bundle via import.meta.env — set real values in a .env file, never
// commit one (see .env.example at the repo root).
const clientId = import.meta.env.VITE_AAD_CLIENT_ID ?? 'TODO-spa-app-registration-client-id';
const tenantId = import.meta.env.VITE_AAD_TENANT_ID ?? 'TODO-tenant-id';
const apiClientId = import.meta.env.VITE_AAD_API_CLIENT_ID ?? 'TODO-api-app-registration-client-id';

export const msalConfig: Configuration = {
  auth: {
    clientId,
    authority: `https://login.microsoftonline.com/${tenantId}`,
    redirectUri: '/',
  },
  cache: {
    cacheLocation: 'sessionStorage',
  },
};

// Scope for the API app registration — matches the "Audience"/"ClientId" the backend's
// Microsoft.Identity.Web config validates tokens against.
export const apiRequest = {
  scopes: [`api://${apiClientId}/access_as_user`],
};
