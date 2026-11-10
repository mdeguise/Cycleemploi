import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import { useLocation } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { useApi } from '../api/ApiContext';
import type { IssueReportDto } from '../api/types';
import { FlagIcon } from '../components/icons';
import { setScreen } from './context';
import { ReportIssueModal, waitingForMe } from './ReportIssueModal';
import './issues.css';

type OpenReport = (edit?: IssueReportDto) => void;

const IssueReporterContext = createContext<OpenReport>(() => undefined);
/** Opens « Mes signalements » on one report (the orange top button). */
const OpenMineContext = createContext<(issueId: number) => void>(() => undefined);

// « ?issue=12 » : lien des courriels à la personne qui a signalé — « Mes signalements » ouvert sur ce signalement (consigne
// de Marc, 2026-11-10). Le lien admin reste /admin/issues?issue=12 (écran Signalements).
const issueFromUrl = window.location.pathname.startsWith('/admin/')
  ? null
  : Number(new URLSearchParams(window.location.search).get('issue')) || null;

/** Owns the "Signaler un problème" modal for the whole signed-in app, so every screen (wizard,
 * batch, Administration) can open it from a button. Must sit inside the router. */
export function IssueReporterProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<{ edit: IssueReportDto | null; mineIssueId?: number | null } | null>(
    issueFromUrl ? { edit: null, mineIssueId: issueFromUrl } : null,
  );
  const location = useLocation();

  // Wizard screens describe themselves (see WizardBody); every other route is named here.
  useEffect(() => {
    const p = location.pathname;
    if (p.startsWith('/admin/')) setScreen(`Administration › ${p.replace('/admin/', '')}`);
    else if (p.startsWith('/lot')) setScreen('Embauche / Réactivation en lot');
  }, [location.pathname]);

  return (
    <IssueReporterContext.Provider value={(edit) => setState({ edit: edit ?? null })}>
      <OpenMineContext.Provider value={(issueId) => setState({ edit: null, mineIssueId: issueId })}>
        {children}
        {state && (
          <ReportIssueModal
            key={state.edit?.issueId ?? (state.mineIssueId ? `mine-${state.mineIssueId}` : 'new')}
            edit={state.edit}
            mineIssueId={state.mineIssueId ?? null}
            onClose={() => setState(null)}
          />
        )}
      </OpenMineContext.Provider>
    </IssueReporterContext.Provider>
  );
}

export function useIssueReporter() {
  return useContext(IssueReporterContext);
}

export function ReportIssueButton({ compact }: { compact?: boolean }) {
  const open = useIssueReporter();
  const openMine = useContext(OpenMineContext);
  const api = useApi();
  // Consigne de Marc (2026-11-10) : dès la connexion, l'usager voit en haut qu'un signalement attend sa réponse.
  const mine = useQuery({ queryKey: ['issues', 'mine'], queryFn: () => api.issues.mine(), refetchInterval: 5 * 60_000, retry: false });
  const waiting = waitingForMe(mine.data);
  const n = waiting.length;
  if (n > 0) {
    const label = `${n} signalement${n > 1 ? 's attendent' : ' attend'} votre réponse`;
    return (
      <button
        type="button"
        className="btn btn-secondary report-btn attente"
        aria-label={label}
        title={waiting
          .map((i) => `#${i.issueId} « ${i.title} »${i.status === 'A_VALIDER' ? ' : correctif à confirmer' : ' : question de l’équipe'}`)
          .join('\n')}
        onClick={() => openMine(waiting[0].issueId)}
      >
        <FlagIcon style={{ width: 15, height: 15 }} />
        <span className="report-count" aria-hidden>{n}</span>
        {!compact && <span className="report-label">{label}</span>}
      </button>
    );
  }
  return (
    <button type="button" className="btn btn-secondary report-btn" title="Signaler un problème" aria-label="Signaler un problème" onClick={() => open()}>
      <FlagIcon style={{ width: 15, height: 15 }} />
      {!compact && <span className="report-label">Signaler un problème</span>}
    </button>
  );
}
