import { useEffect } from 'react';
import { useIsAuthenticated, useMsal } from '@azure/msal-react';
import { InteractionStatus } from '@azure/msal-browser';
import type { ReactNode } from 'react';

/**
 * Gates the whole app behind Entra ID sign-in. Redirects to login automatically rather than
 * showing a "please sign in" button — this app has no meaningful unauthenticated state, unlike a
 * public site where a sign-in prompt makes sense.
 */
export function AuthGate({ children }: { children: ReactNode }) {
  const { instance, accounts, inProgress } = useMsal();
  const isAuthenticated = useIsAuthenticated();

  useEffect(() => {
    if (!isAuthenticated && inProgress === InteractionStatus.None) {
      instance.loginRedirect();
    }
  }, [isAuthenticated, inProgress, instance]);

  useEffect(() => {
    if (accounts.length > 0 && !instance.getActiveAccount()) {
      instance.setActiveAccount(accounts[0]);
    }
  }, [accounts, instance]);

  if (!isAuthenticated) {
    return (
      <div style={{ display: 'flex', height: '100vh', alignItems: 'center', justifyContent: 'center' }}>
        Redirection vers la connexion…
      </div>
    );
  }

  return <>{children}</>;
}
