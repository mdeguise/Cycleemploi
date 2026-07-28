import type { IPublicClientApplication } from '@azure/msal-browser';
import { apiRequest } from '../auth/authConfig';

// TODO Phase 0/5: in Docker, /api is same-origin (the web container's Nginx reverse-proxies it to
// the api container — see the plan's Docker/deployment section), so this should stay empty/relative
// in production. VITE_API_BASE_URL lets local dev point at a directly-running backend instead.
const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '';

export class ApiError extends Error {
  status: number;

  constructor(status: number, message: string) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
  }
}

/**
 * Thin fetch wrapper that attaches the current user's MSAL access token to every request.
 * `msalInstance` is passed in rather than imported as a singleton so this stays testable and so
 * App.tsx controls MSAL's lifecycle explicitly.
 */
export function createApiClient(msalInstance: IPublicClientApplication) {
  async function request<T>(path: string, init?: RequestInit): Promise<T> {
    const account = msalInstance.getActiveAccount();
    if (!account) {
      throw new ApiError(401, 'No signed-in account.');
    }

    const tokenResponse = await msalInstance.acquireTokenSilent({
      ...apiRequest,
      account,
    });

    const response = await fetch(`${API_BASE_URL}${path}`, {
      ...init,
      headers: {
        ...(init?.body ? { 'Content-Type': 'application/json' } : {}),
        Authorization: `Bearer ${tokenResponse.accessToken}`,
        ...init?.headers,
      },
    });

    if (!response.ok) {
      const text = await response.text().catch(() => '');
      throw new ApiError(response.status, text || response.statusText);
    }

    if (response.status === 204) {
      return undefined as T;
    }

    return (await response.json()) as T;
  }

  return {
    get: <T>(path: string) => request<T>(path),
    post: <T>(path: string, body?: unknown) =>
      request<T>(path, { method: 'POST', body: body ? JSON.stringify(body) : undefined }),
    put: <T>(path: string, body?: unknown) =>
      request<T>(path, { method: 'PUT', body: body ? JSON.stringify(body) : undefined }),
  };
}

export type ApiClient = ReturnType<typeof createApiClient>;
