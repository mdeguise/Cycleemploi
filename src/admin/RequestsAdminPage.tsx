import { useCallback, useEffect, useMemo, useState } from 'react';
import { useApi } from '../api/ApiContext';
import type {
  AdminRequestDetailDto,
  AdminRequestSummaryDto,
  RequestTicketDto,
  TicketViewRowDto,
} from '../api/types';
import { TicketsTableView } from './TicketsTableView';

const STATUS_LABELS: Record<string, string> = {
  Brouillon: 'Brouillon',
  Soumise: 'Soumise',
  EnTraitement: 'En traitement',
  Completee: 'Complétée',
};

const TYPE_LABELS: Record<string, string> = {
  Onboarding: 'Nouvelle intégration',
  Reactivation: 'Réactivation',
  Offboarding: 'Cessation',
  D365AccessOnly: 'Accès D365 (directe)',
};

const dateOnly = (v?: string | null) => (v ? new Date(v).toLocaleDateString('fr-CA') : '—');
const dateTime = (v?: string | null) => (v ? new Date(v).toLocaleString('fr-CA') : '—');

/** Only renders a row when there is something to show — an admin reviewing a request should see the
 *  fields that were filled in, not a wall of "—". */
function Field({ label, value }: { label: string; value?: string | null }) {
  if (value === null || value === undefined || value === '') return null;
  return (
    <div style={{ display: 'flex', gap: 8, padding: '2px 0', fontSize: 13 }}>
      <div style={{ minWidth: 190, color: 'var(--muted, #666)' }}>{label}</div>
      <div style={{ flex: 1, whiteSpace: 'pre-wrap' }}>{value}</div>
    </div>
  );
}

function ListField({ label, values }: { label: string; values?: string[] | null }) {
  if (!values || values.length === 0) return null;
  return <Field label={label} value={values.join(', ')} />;
}

function TicketRow({
  ticket,
  canRetry,
  isRetrying,
  onRetry,
}: {
  ticket: RequestTicketDto;
  canRetry: boolean;
  isRetrying: boolean;
  onRetry: (t: RequestTicketDto) => void;
}) {
  const failed = ticket.outcome === 'Failed';
  return (
    <tr style={{ borderBottom: '1px solid var(--border, #eee)' }}>
      <td style={{ padding: '6px 10px' }}>
        {ticket.kindLabel}
        {ticket.employeeName && (
          <div style={{ color: 'var(--muted, #666)', fontSize: 12 }}>{ticket.employeeName}</div>
        )}
      </td>
      <td style={{ padding: '6px 10px', whiteSpace: 'nowrap' }}>
        <span
          style={{
            padding: '2px 8px',
            borderRadius: 10,
            fontSize: 12,
            background: failed ? 'var(--tremblant-red-dark, #b00)' : 'var(--ok-bg, #1c7c3c)',
            color: '#fff',
          }}
        >
          {failed ? 'Échec' : 'Créé'}
        </span>
      </td>
      <td style={{ padding: '6px 10px', fontFamily: 'var(--mono)' }}>{ticket.ticketNumber ?? '—'}</td>
      <td style={{ padding: '6px 10px', fontSize: 12, maxWidth: 380 }}>
        {failed ? (
          <>
            <div style={{ fontWeight: 600 }}>{ticket.errorType}</div>
            <div style={{ color: 'var(--muted, #666)' }}>{ticket.errorMessage}</div>
          </>
        ) : (
          '—'
        )}
      </td>
      <td style={{ padding: '6px 10px', textAlign: 'center' }}>{ticket.attemptCount}</td>
      <td style={{ padding: '6px 10px', whiteSpace: 'nowrap', fontSize: 12 }}>
        {dateTime(ticket.lastAttemptAt)}
      </td>
      <td style={{ padding: '6px 10px', textAlign: 'right' }}>
        {failed && canRetry && (
          <button
            type="button"
            className="btn btn-primary"
            disabled={isRetrying}
            onClick={() => onRetry(ticket)}
          >
            {isRetrying ? 'En cours…' : 'Réessayer'}
          </button>
        )}
      </td>
    </tr>
  );
}

export function RequestsAdminPage() {
  const api = useApi();

  const [items, setItems] = useState<AdminRequestSummaryDto[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const pageSize = 25;

  const [q, setQ] = useState('');
  const [status, setStatus] = useState('');
  const [requestType, setRequestType] = useState('');
  const [onlyFailures, setOnlyFailures] = useState(false);

  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  // 'tableau' is the default: the operational question is "what are the ticket numbers and which
  // are still open", which the flat table answers at a glance. 'detail' is for reading one request.
  const [view, setView] = useState<'tableau' | 'detail'>('tableau');
  const [ticketRows, setTicketRows] = useState<TicketViewRowDto[]>([]);
  const [hasUnknownStatuses, setHasUnknownStatuses] = useState(false);

  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [detail, setDetail] = useState<AdminRequestDetailDto | null>(null);
  const [detailError, setDetailError] = useState<string | null>(null);
  const [retryingId, setRetryingId] = useState<number | null>(null);
  const [retryMessage, setRetryMessage] = useState<{ ok: boolean; text: string } | null>(null);

  const loadList = useCallback(() => {
    setIsLoading(true);
    setLoadError(null);
    const params = { q, status, requestType, onlyFailures, page, pageSize };

    // The table view costs outbound calls to Freshdesk/TDX for live status, so it is only fetched
    // when that view is actually on screen.
    const work =
      view === 'tableau'
        ? api.adminRequests.ticketView(params).then((res) => {
            setTicketRows(res.items);
            setHasUnknownStatuses(res.hasUnknownStatuses);
            setTotal(res.total);
          })
        : api.adminRequests.list(params).then((res) => {
            setItems(res.items);
            setTotal(res.total);
          });

    work
      .catch((err) => setLoadError(err instanceof Error ? err.message : 'Erreur inconnue'))
      .finally(() => setIsLoading(false));
  }, [api, q, status, requestType, onlyFailures, page, view]);

  // Debounced so typing in the search box doesn't fire a request per keystroke.
  useEffect(() => {
    const timer = setTimeout(loadList, 250);
    return () => clearTimeout(timer);
  }, [loadList]);

  const loadDetail = useCallback(
    (id: number) => {
      setDetailError(null);
      setRetryMessage(null);
      api.adminRequests
        .detail(id)
        .then(setDetail)
        .catch((err) => {
          setDetail(null);
          setDetailError(err instanceof Error ? err.message : 'Erreur inconnue');
        });
    },
    [api],
  );

  useEffect(() => {
    if (selectedId !== null) loadDetail(selectedId);
  }, [selectedId, loadDetail]);

  const handleRetry = async (ticket: RequestTicketDto) => {
    if (selectedId === null) return;
    setRetryingId(ticket.requestTicketId);
    setRetryMessage(null);
    try {
      const res = await api.adminRequests.retryTicket(selectedId, ticket.requestTicketId);
      setRetryMessage(
        res.succeeded
          ? { ok: true, text: `Billet créé : ${res.ticketNumber}` }
          : { ok: false, text: res.error ?? 'Échec de la relance.' },
      );
      // Reload both: the ticket row changed, and so did the request's failure count in the list.
      loadDetail(selectedId);
      loadList();
    } catch (err) {
      setRetryMessage({ ok: false, text: err instanceof Error ? err.message : 'Erreur inconnue' });
    } finally {
      setRetryingId(null);
    }
  };

  const r = detail?.request;
  const isOffboarding = r?.requestType === 'Offboarding';
  const pageCount = Math.max(1, Math.ceil(total / pageSize));

  const failuresInView = useMemo(() => items.reduce((n, i) => n + i.ticketsFailed, 0), [items]);

  return (
    <div className="step-panel">
      <div className="step-panel__header">
        <div>
          <div className="step-panel__title">Demandes</div>
          <div className="step-panel__subtitle">
            Toutes les demandes soumises, avec les billets créés dans chaque système. Lorsqu'un billet
            a échoué, « Réessayer » relance <strong>uniquement celui-là</strong> — les billets déjà
            créés ne sont jamais recréés en double.
          </div>
        </div>
      </div>

      {/* Filters */}
      <div style={{ display: 'flex', gap: 12, alignItems: 'flex-end', flexWrap: 'wrap', marginBottom: 12 }}>
        <div className="field" style={{ minWidth: 260 }}>
          <label className="field__label">Rechercher</label>
          <div className="field__input-wrap">
            <input
              type="text"
              value={q}
              onChange={(ev) => {
                setPage(1);
                setQ(ev.target.value);
              }}
              placeholder="Numéro, demandeur ou employé"
            />
          </div>
        </div>
        <div className="field">
          <label className="field__label">Statut</label>
          <div className="field__input-wrap">
            <select value={status} onChange={(ev) => { setPage(1); setStatus(ev.target.value); }}>
              <option value="">Tous</option>
              {Object.entries(STATUS_LABELS).map(([k, v]) => (
                <option key={k} value={k}>{v}</option>
              ))}
            </select>
          </div>
        </div>
        <div className="field">
          <label className="field__label">Type</label>
          <div className="field__input-wrap">
            <select value={requestType} onChange={(ev) => { setPage(1); setRequestType(ev.target.value); }}>
              <option value="">Tous</option>
              {Object.entries(TYPE_LABELS).map(([k, v]) => (
                <option key={k} value={k}>{v}</option>
              ))}
            </select>
          </div>
        </div>
        <label style={{ display: 'flex', alignItems: 'center', gap: 6, paddingBottom: 8 }}>
          <input
            type="checkbox"
            checked={onlyFailures}
            onChange={(ev) => { setPage(1); setOnlyFailures(ev.target.checked); }}
          />
          Seulement les demandes avec un billet en échec
        </label>
        <div style={{ display: 'flex', gap: 4, paddingBottom: 8 }}>
          <button
            type="button"
            className={view === 'tableau' ? 'btn btn-primary' : 'btn btn-secondary'}
            onClick={() => setView('tableau')}
          >
            Vue tableau
          </button>
          <button
            type="button"
            className={view === 'detail' ? 'btn btn-primary' : 'btn btn-secondary'}
            onClick={() => setView('detail')}
          >
            Vue détaillée
          </button>
        </div>

        <div style={{ paddingBottom: 8, color: 'var(--muted, #666)', fontSize: 13 }}>
          {total} demande{total > 1 ? 's' : ''}
          {failuresInView > 0 && ` — ${failuresInView} billet(s) en échec sur cette page`}
        </div>
      </div>

      {loadError && <div className="big-notice">{loadError}</div>}

      {view === 'tableau' && (
        isLoading ? (
          <div style={{ padding: 12 }}>Chargement… (statuts récupérés depuis Freshdesk et TDX)</div>
        ) : (
          <TicketsTableView
            rows={ticketRows}
            hasUnknownStatuses={hasUnknownStatuses}
            onSelect={(id) => {
              setSelectedId(id);
              setView('detail');
            }}
          />
        )
      )}

      {/* Two panes, each scrolling on its own, so the screen never grows past one viewport. */}
      {view === 'detail' && (
      <div style={{ display: 'grid', gridTemplateColumns: 'minmax(320px, 420px) 1fr', gap: 16, alignItems: 'start' }}>
        <div style={{ maxHeight: '62vh', overflowY: 'auto', border: '1px solid var(--border, #ddd)' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse' }}>
            <tbody>
              {isLoading && (
                <tr><td style={{ padding: 12 }}>Chargement…</td></tr>
              )}
              {!isLoading && items.length === 0 && (
                <tr><td style={{ padding: 12 }}>Aucune demande ne correspond aux filtres.</td></tr>
              )}
              {items.map((it) => (
                <tr
                  key={it.requestId}
                  onClick={() => setSelectedId(it.requestId)}
                  style={{
                    cursor: 'pointer',
                    borderBottom: '1px solid var(--border, #eee)',
                    background: it.requestId === selectedId ? 'var(--selected-bg, #eef4ff)' : undefined,
                  }}
                >
                  <td style={{ padding: '8px 10px' }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8 }}>
                      <strong>{it.requestNumber}</strong>
                      {it.ticketsFailed > 0 && (
                        <span style={{ background: 'var(--tremblant-red-dark, #b00)', color: '#fff', borderRadius: 10, padding: '1px 8px', fontSize: 12 }}>
                          {it.ticketsFailed} à corriger
                        </span>
                      )}
                    </div>
                    <div style={{ fontSize: 13 }}>{it.employeeNames.join(', ') || '—'}</div>
                    <div style={{ fontSize: 12, color: 'var(--muted, #666)' }}>
                      {TYPE_LABELS[it.requestType] ?? it.requestType} · {STATUS_LABELS[it.status] ?? it.status} ·{' '}
                      {dateOnly(it.submittedAt ?? it.createdAt)} · {it.demandePar}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <div style={{ maxHeight: '62vh', overflowY: 'auto' }}>
          {detailError && <div className="big-notice">{detailError}</div>}
          {!detail && !detailError && (
            <div style={{ color: 'var(--muted, #666)', padding: 12 }}>
              Sélectionnez une demande pour voir les informations saisies et ses billets.
            </div>
          )}

          {detail && r && (
            <>
              <h3 style={{ margin: '0 0 4px' }}>
                {r.requestNumber} — {TYPE_LABELS[r.requestType] ?? r.requestType}
              </h3>
              <div style={{ color: 'var(--muted, #666)', fontSize: 13, marginBottom: 12 }}>
                Demandé par {r.demandePar}
                {detail.requesterEmail ? ` (${detail.requesterEmail})` : ' — aucun courriel enregistré'} ·
                soumise le {dateOnly(detail.submittedAt)}
              </div>

              <h4 style={{ margin: '12px 0 4px' }}>Billets</h4>
              {retryMessage && (
                <div
                  className="big-notice"
                  style={{ color: retryMessage.ok ? undefined : 'var(--tremblant-red-dark)' }}
                >
                  {retryMessage.text}
                </div>
              )}
              {detail.tickets.length === 0 ? (
                <div style={{ color: 'var(--muted, #666)', fontSize: 13 }}>
                  Aucun billet enregistré. Les demandes soumises avant l'ajout de ce suivi n'en ont pas.
                </div>
              ) : (
                <table style={{ width: '100%', borderCollapse: 'collapse', marginBottom: 16 }}>
                  <thead>
                    <tr style={{ textAlign: 'left', borderBottom: '2px solid var(--border, #ddd)', fontSize: 12 }}>
                      <th style={{ padding: '6px 10px' }}>Système</th>
                      <th style={{ padding: '6px 10px' }}>État</th>
                      <th style={{ padding: '6px 10px' }}>N° de billet</th>
                      <th style={{ padding: '6px 10px' }}>Erreur</th>
                      <th style={{ padding: '6px 10px' }}>Tent.</th>
                      <th style={{ padding: '6px 10px' }}>Dernière tentative</th>
                      <th style={{ padding: '6px 10px' }}></th>
                    </tr>
                  </thead>
                  <tbody>
                    {detail.tickets.map((t) => (
                      <TicketRow
                        key={t.requestTicketId}
                        ticket={t}
                        canRetry={detail.canRetry}
                        isRetrying={retryingId === t.requestTicketId}
                        onRetry={handleRetry}
                      />
                    ))}
                  </tbody>
                </table>
              )}

              <h4 style={{ margin: '12px 0 4px' }}>Informations saisies</h4>
              <Field label="Employé(s)" value={r.employees.map((e) => `${e.nameSnapshot} (${e.workdayEmployeeId})`).join(', ')} />
              <Field label="Poste" value={r.employees[0]?.positionSnapshot} />
              <Field label="Département" value={r.employees[0]?.departementSnapshot} />
              <Field label="Gestionnaire" value={r.employees[0]?.gestionnaireSnapshot} />
              <Field label="Code d'emploi" value={r.employees[0]?.codeEmploiSnapshot} />
              <Field label="Type d'emploi" value={r.employees[0]?.typeEmploiSnapshot} />

              {!isOffboarding && (
                <>
                  <Field label="Date d'entrée prévue" value={dateOnly(r.dateEntreePrevue)} />
                  <Field label="Règle de paye" value={r.regleDePaye} />
                  <Field label="Précision règle de paye" value={r.regleDePayeCommentaire} />
                </>
              )}
              {isOffboarding && (
                <>
                  <Field label="Dernière journée" value={dateOnly(r.derniereJournee)} />
                  <Field label="Indemnité de vacances" value={r.indemniteVacances} />
                  <Field label="Raison de l'arrêt" value={r.raisonArret} />
                  <Field label="Détails de la raison" value={r.detailsRaison} />
                  <Field label="Réembaucheriez-vous" value={r.reembaucheriez} />
                  <Field label="Date de retour connue" value={r.dateRetourConnue} />
                  <Field label="Date de retour au travail" value={dateOnly(r.dateRetourTravail)} />
                  <Field label="Préavis reçu" value={r.preavisRecu} />
                  <Field label="Motif de non-admissibilité" value={r.motifNonAdmissibilite} />
                </>
              )}

              <ListField label="Systèmes d'accès" values={r.systemesAcces} />
              <Field label="Zones de badge" value={r.badgeZones} />
              <Field label="Détails code d'alarme" value={r.codeAlarmeDetails} />
              <ListField label="POS / Hébergement" values={r.systemePosHebergement} />
              <Field label="Stationnement" value={r.stationnementRequis} />
              <Field label="Justification des accès" value={r.justificationAcces} />
              <ListField label="Équipements" values={r.equipements} />
              <Field label="Notes équipement" value={r.notesEquipement} />
              <ListField label="Applications" values={r.applications} />
              <Field label="Autre logiciel" value={r.autreLogicielRequis} />
              <Field label="Commentaires TI" value={r.commentairesIT} />
              <Field label="Commentaires stationnement" value={r.commentairesStationnement} />
              <Field label="Commentaires puce d'accès" value={r.commentairesPuceAcces} />
              <Field label="Commentaires Redingote" value={r.commentairesRedingote} />

              {/* Present only when the API decided this caller may read it — see
                  AdminRequestsController.Detail. */}
              {r.commentairesRH != null && r.commentairesRH !== '' && (
                <div style={{ marginTop: 12, padding: 10, border: '1px solid var(--tremblant-red-dark, #b00)' }}>
                  <div style={{ fontWeight: 600, fontSize: 13 }}>Commentaire RH (confidentiel)</div>
                  <div style={{ whiteSpace: 'pre-wrap', fontSize: 13 }}>{r.commentairesRH}</div>
                </div>
              )}
            </>
          )}
        </div>
      </div>
      )}

      {pageCount > 1 && (
        <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginTop: 12 }}>
          <button type="button" className="review-section__edit" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
            Précédent
          </button>
          <span style={{ fontSize: 13 }}>Page {page} / {pageCount}</span>
          <button type="button" className="review-section__edit" disabled={page >= pageCount} onClick={() => setPage((p) => p + 1)}>
            Suivant
          </button>
        </div>
      )}
    </div>
  );
}
