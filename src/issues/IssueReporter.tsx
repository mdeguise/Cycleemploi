import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import { useLocation } from 'react-router-dom';
import type { IssueReportDto } from '../api/types';
import { FlagIcon } from '../components/icons';
import { setScreen } from './context';
import { ReportIssueModal } from './ReportIssueModal';
import './issues.css';

type OpenReport = (edit?: IssueReportDto) => void;

const IssueReporterContext = createContext<OpenReport>(() => undefined);

/** Owns the "Signaler un problème" modal for the whole signed-in app, so every screen (wizard,
 * batch, Administration) can open it from a button. Must sit inside the router. */
export function IssueReporterProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<{ edit: IssueReportDto | null } | null>(null);
  const location = useLocation();

  // Wizard screens describe themselves (see WizardBody); every other route is named here.
  useEffect(() => {
    const p = location.pathname;
    if (p.startsWith('/admin/')) setScreen(`Administration › ${p.replace('/admin/', '')}`);
    else if (p.startsWith('/lot')) setScreen('Embauche en lot');
  }, [location.pathname]);

  return (
    <IssueReporterContext.Provider value={(edit) => setState({ edit: edit ?? null })}>
      {children}
      {state && <ReportIssueModal key={state.edit?.issueId ?? 'new'} edit={state.edit} onClose={() => setState(null)} />}
    </IssueReporterContext.Provider>
  );
}

export function useIssueReporter() {
  return useContext(IssueReporterContext);
}

export function ReportIssueButton({ compact }: { compact?: boolean }) {
  const open = useIssueReporter();
  return (
    <button type="button" className="btn btn-secondary report-btn" title="Signaler un problème" onClick={() => open()}>
      <FlagIcon style={{ width: 15, height: 15 }} />
      {!compact && <span className="report-label">Signaler un problème</span>}
    </button>
  );
}
