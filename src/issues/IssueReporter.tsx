import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react';
import { useLocation } from 'react-router-dom';
import { FlagIcon } from '../components/icons';
import { setScreen } from './context';
import Signalement from './Signalement';
import './issues.css';

type OpenReport = () => void;

const IssueReporterContext = createContext<OpenReport>(() => undefined);

/** Owns the "Signaler un problème" modal for the whole signed-in app, so every screen (wizard,
 * batch, Administration) can open it from a button. Must sit inside the router. */
export function IssueReporterProvider({ children }: { children: ReactNode }) {
  const [ouvert, setOuvert] = useState(false);
  const fermer = useCallback(() => setOuvert(false), []);
  const location = useLocation();

  // Wizard screens describe themselves (see WizardBody); every other route is named here.
  useEffect(() => {
    const p = location.pathname;
    if (p.startsWith('/admin/')) setScreen(`Administration › ${p.replace('/admin/', '')}`);
    else if (p.startsWith('/lot')) setScreen('Embauche / Réactivation en lot');
  }, [location.pathname]);

  return (
    <IssueReporterContext.Provider value={() => setOuvert(true)}>
      {children}
      {ouvert && <Signalement onClose={fermer} />}
    </IssueReporterContext.Provider>
  );
}

export function useIssueReporter() {
  return useContext(IssueReporterContext);
}

export function ReportIssueButton({ compact }: { compact?: boolean }) {
  const open = useIssueReporter();
  return (
    <button type="button" className="btn btn-secondary report-btn" title="Signaler un problème" aria-label="Signaler un problème" onClick={() => open()}>
      <FlagIcon style={{ width: 15, height: 15 }} />
      {!compact && <span className="report-label">Signaler un problème</span>}
    </button>
  );
}
