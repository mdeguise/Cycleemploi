import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useApi } from '../api/ApiContext';
import type { D365AccessApprovalSummaryDto, MeDto } from '../api/types';

const TDX_TICKET_URL = 'https://get.alterra.support/TDNext/Apps/278/Tickets/TicketDet?TicketID=';

export function D365ApprovalsListPage({ me }: { me: MeDto }) {
  // A D365Viewer who isn't ALSO a D365Approver can open a pending request but never act on it —
  // the link still goes to the same form page (canComplete drives the read-only state there), only
  // the label changes so "Personnel TI" isn't invited to fill out something they can't send.
  const canAct = me.isD365Approver;
  const api = useApi();
  const [rows, setRows] = useState<D365AccessApprovalSummaryDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  useEffect(() => {
    setIsLoading(true);
    setLoadError(null);
    api.d365AccessApprovals
      .list()
      .then(setRows)
      .catch((err) => setLoadError(err instanceof Error ? err.message : 'Erreur inconnue'))
      .finally(() => setIsLoading(false));
  }, [api]);

  const pending = rows.filter((r) => r.status === 'Pending');
  const completed = rows.filter((r) => r.status !== 'Pending');

  const renderTicketState = (r: D365AccessApprovalSummaryDto) => {
    if (r.ticketNumber) {
      return (
        <>
          <a href={`${TDX_TICKET_URL}${r.ticketNumber}`} target="_blank" rel="noreferrer">
            #{r.ticketNumber}
          </a>
          {r.ticketStateLabel && <span style={{ marginLeft: 8, color: 'var(--muted)' }}>({r.ticketStateLabel})</span>}
        </>
      );
    }
    if (r.ticketState === 'Failed') {
      return <span style={{ color: 'var(--tremblant-red-dark)' }}>Échec de création — voir Administration &gt; Demandes</span>;
    }
    return '—';
  };

  return (
    <div className="step-panel">
      <div className="step-panel__header">
        <div>
          <div className="step-panel__title">Approbations D365</div>
          <div className="step-panel__subtitle">
            Chaque demande de Cycle Emploi ayant sélectionné l'accès D365, en attente ou complétée, avec un lien
            direct vers le billet TDX qui en résulte.
          </div>
        </div>
      </div>

      {isLoading && <div>Chargement…</div>}
      {loadError && <div className="big-notice">{loadError}</div>}

      {!isLoading && (
        <>
          <div className="field-section-title">En attente ({pending.length})</div>
          <div style={{ overflowX: 'auto', marginBottom: 24 }}>
            <table style={{ width: '100%', borderCollapse: 'collapse' }}>
              <thead>
                <tr style={{ textAlign: 'left', borderBottom: '2px solid var(--border, #ddd)' }}>
                  <th style={{ padding: '8px 12px' }}>Demande</th>
                  <th style={{ padding: '8px 12px' }}>Employé</th>
                  <th style={{ padding: '8px 12px' }}>Titre de poste</th>
                  <th style={{ padding: '8px 12px' }}>Gestionnaire</th>
                  <th style={{ padding: '8px 12px' }}>Demandé par</th>
                  <th style={{ padding: '8px 12px' }}>Date de début</th>
                  <th style={{ padding: '8px 12px' }}>Demandée le</th>
                  <th style={{ padding: '8px 12px' }}></th>
                </tr>
              </thead>
              <tbody>
                {pending.length === 0 && (
                  <tr>
                    <td colSpan={8} style={{ padding: '8px 12px', color: 'var(--muted)' }}>Aucune approbation en attente.</td>
                  </tr>
                )}
                {pending.map((r) => (
                  <tr key={r.requestId} style={{ borderBottom: '1px solid var(--border, #eee)' }}>
                    <td style={{ padding: '8px 12px' }}>{r.requestNumber}</td>
                    <td style={{ padding: '8px 12px' }}>{r.employeeName}</td>
                    <td style={{ padding: '8px 12px' }}>{r.positionTitle ?? '—'}</td>
                    <td style={{ padding: '8px 12px' }}>{r.managerName ?? '—'}</td>
                    <td style={{ padding: '8px 12px' }}>{r.requesterName}</td>
                    <td style={{ padding: '8px 12px' }}>{r.startDate ? new Date(r.startDate).toLocaleDateString('fr-CA') : '—'}</td>
                    <td style={{ padding: '8px 12px' }}>{new Date(r.createdAt).toLocaleDateString('fr-CA')}</td>
                    <td style={{ padding: '8px 12px', textAlign: 'right' }}>
                      <Link className="review-section__edit" to={`/admin/d365-approvals/${r.requestId}`}>
                        {canAct ? 'Remplir le formulaire' : 'Voir les détails'}
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="field-section-title">Complétées ({completed.length})</div>
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse' }}>
              <thead>
                <tr style={{ textAlign: 'left', borderBottom: '2px solid var(--border, #ddd)' }}>
                  <th style={{ padding: '8px 12px' }}>Demande</th>
                  <th style={{ padding: '8px 12px' }}>Employé</th>
                  <th style={{ padding: '8px 12px' }}>Gestionnaire</th>
                  <th style={{ padding: '8px 12px' }}>Demandé par</th>
                  <th style={{ padding: '8px 12px' }}>Date de début</th>
                  <th style={{ padding: '8px 12px' }}>Complétée par</th>
                  <th style={{ padding: '8px 12px' }}>Complétée le</th>
                  <th style={{ padding: '8px 12px' }}>Billet TDX</th>
                </tr>
              </thead>
              <tbody>
                {completed.length === 0 && (
                  <tr>
                    <td colSpan={8} style={{ padding: '8px 12px', color: 'var(--muted)' }}>Aucune approbation complétée.</td>
                  </tr>
                )}
                {completed.map((r) => (
                  <tr key={r.requestId} style={{ borderBottom: '1px solid var(--border, #eee)' }}>
                    <td style={{ padding: '8px 12px' }}>{r.requestNumber}</td>
                    <td style={{ padding: '8px 12px' }}>{r.employeeName}</td>
                    <td style={{ padding: '8px 12px' }}>{r.managerName ?? '—'}</td>
                    <td style={{ padding: '8px 12px' }}>{r.requesterName}</td>
                    <td style={{ padding: '8px 12px' }}>{r.startDate ? new Date(r.startDate).toLocaleDateString('fr-CA') : '—'}</td>
                    <td style={{ padding: '8px 12px' }}>{r.completedByDisplayName ?? '—'}</td>
                    <td style={{ padding: '8px 12px' }}>{r.completedAt ? new Date(r.completedAt).toLocaleDateString('fr-CA') : '—'}</td>
                    <td style={{ padding: '8px 12px' }}>{renderTicketState(r)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}
    </div>
  );
}
