import { useEffect, useMemo, useState } from 'react';
import { useApi } from '../api/ApiContext';
import type { D365ApproverDto, D365PositionTitleDto, MeDto } from '../api/types';

/** The bare sAMAccountName from a Windows identity like "ENTERPRISE\\mdeguise" (or an email/plain
 * sam), lowercased — mirrors the backend's AppUserService.Normalize so a client-side "is this my
 * own row" check agrees with the server's. */
function bareSam(identity: string): string {
  const afterSlash = identity.includes('\\') ? identity.split('\\').pop()! : identity;
  const beforeAt = afterSlash.includes('@') ? afterSlash.split('@')[0] : afterSlash;
  return beforeAt.toLowerCase();
}

/** One row per distinct Workday Position_Title, showing which D365Approver(s) are scoped
 * specifically to it. A title with none shown falls back to whichever approvers are GLOBAL (no
 * Position Title at all) — see Approbateurs D365 for managing those.
 *
 * Multiple approvers per title is intentional, same as everywhere else in this app (any one
 * matched approver acting is enough) — the per-row "M'assigner ce titre" button only ever ADDS
 * the caller alongside whoever is already there, it never replaces them; the Approbateur column
 * already lists every name as its own removable chip.
 *
 * Self-assign lives directly in the table row (one click, no title dropdown to align with first)
 * — a standalone dropdown+button above a 300+ row table was easy to lose track of. Adding someone
 * ELSE as an approver is handled entirely by the separate "Approbateurs D365" screen now; this
 * page is self-service only (assign/remove your own row per title). */
export function D365PositionTitleAssignmentsPage({ me }: { me: MeDto }) {
  const api = useApi();
  const isAdmin = me.isTicketTemplateAdmin;
  const isApprover = me.isD365Approver;
  const mySam = bareSam(me.objectId);

  const [titles, setTitles] = useState<D365PositionTitleDto[]>([]);
  const [approvers, setApprovers] = useState<D365ApproverDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [filter, setFilter] = useState('');
  const [assigningTitle, setAssigningTitle] = useState<string | null>(null);
  const [removingTitle, setRemovingTitle] = useState<string | null>(null);

  const load = () => {
    setIsLoading(true);
    setLoadError(null);
    Promise.all([api.d365Approvers.positionTitles(), api.d365Approvers.list()])
      .then(([t, a]) => {
        setTitles(t);
        setApprovers(a);
      })
      .catch((err) => setLoadError(err instanceof Error ? err.message : 'Erreur inconnue'))
      .finally(() => setIsLoading(false));
  };

  useEffect(load, [api]);

  const globalCount = approvers.filter((a) => !a.positionTitle).length;
  const filteredTitles = useMemo(() => {
    const q = filter.trim().toLowerCase();
    return q
      ? titles.filter(
          (t) =>
            t.positionTitle.toLowerCase().includes(q) ||
            t.jobCodes.some((code) => code.toLowerCase().includes(q)),
        )
      : titles;
  }, [titles, filter]);

  const handleSelfAssign = async (title: string) => {
    setLoadError(null);
    setAssigningTitle(title);
    try {
      await api.d365Approvers.add({
        sam: me.objectId,
        displayName: me.displayName,
        email: me.email ?? null,
        positionTitle: title,
      });
      load();
    } catch (err) {
      setLoadError(err instanceof Error ? err.message : 'Erreur inconnue');
    } finally {
      setAssigningTitle(null);
    }
  };

  const handleRemove = async (id: number) => {
    setLoadError(null);
    try {
      await api.d365Approvers.remove(id);
      load();
    } catch (err) {
      setLoadError(err instanceof Error ? err.message : 'Erreur inconnue');
    }
  };

  const handleSelfRemove = async (title: string, id: number) => {
    setLoadError(null);
    setRemovingTitle(title);
    try {
      await api.d365Approvers.remove(id);
      load();
    } catch (err) {
      setLoadError(err instanceof Error ? err.message : 'Erreur inconnue');
    } finally {
      setRemovingTitle(null);
    }
  };

  return (
    <div className="step-panel">
      <div className="step-panel__header">
        <div>
          <div className="step-panel__title">Titres de poste</div>
          <div className="step-panel__subtitle">
            Un titre de poste sans approbateur assigné dans le tableau ci-dessous utilise les approbateurs{' '}
            <strong>globaux</strong> ({globalCount === 0 ? 'aucun configuré pour l\'instant' : `${globalCount} présentement`}
            {' '}— voir Approbateurs D365).
            {isApprover && ' Cliquez « M\'assigner ce titre » sur une ligne pour vous ajouter comme approbateur.'}
          </div>
        </div>
      </div>

      {isLoading && <div>Chargement…</div>}
      {loadError && <div className="big-notice">{loadError}</div>}

      {!isLoading && (
        <>
          <div className="field" style={{ maxWidth: 380, marginBottom: 10 }}>
            <div className="field__input-wrap">
              <input
                type="text"
                value={filter}
                onChange={(ev) => setFilter(ev.target.value)}
                placeholder="Filtrer par titre de poste…"
              />
            </div>
          </div>

          <div style={{ overflow: 'auto', maxHeight: 460, border: '1px solid var(--border, #ddd)' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse' }}>
              <thead>
                <tr style={{ textAlign: 'left', borderBottom: '2px solid var(--border, #ddd)' }}>
                  <th style={{ padding: '8px 12px', position: 'sticky', top: 0, background: 'var(--bg-card, #fff)' }}>Titre de poste ({titles.length})</th>
                  <th style={{ padding: '8px 12px', position: 'sticky', top: 0, background: 'var(--bg-card, #fff)' }}>Code(s) d'emploi</th>
                  <th style={{ padding: '8px 12px', position: 'sticky', top: 0, background: 'var(--bg-card, #fff)' }}>Approbateur</th>
                  {isApprover && <th style={{ padding: '8px 12px', position: 'sticky', top: 0, background: 'var(--bg-card, #fff)' }}></th>}
                </tr>
              </thead>
              <tbody>
                {filteredTitles.length === 0 && (
                  <tr>
                    <td colSpan={isApprover ? 4 : 3} style={{ padding: '8px 12px', color: 'var(--muted)' }}>
                      {titles.length === 0 ? 'Aucun titre de poste trouvé.' : 'Aucun titre ne correspond au filtre.'}
                    </td>
                  </tr>
                )}
                {filteredTitles.map((row) => {
                  const title = row.positionTitle;
                  const scoped = approvers.filter((a) => a.positionTitle === title);
                  const isMine = scoped.some((a) => a.sam === mySam);
                  return (
                    <tr key={title} style={{ borderBottom: '1px solid var(--border, #eee)' }}>
                      <td style={{ padding: '8px 12px' }}>{title}</td>
                      <td style={{ padding: '8px 12px', color: 'var(--muted)', fontFamily: 'var(--mono)', fontSize: '0.9em' }}>
                        {row.jobCodes.length > 0 ? row.jobCodes.join(', ') : '—'}
                      </td>
                      <td style={{ padding: '8px 12px' }}>
                        {scoped.length === 0 ? (
                          <span style={{ color: 'var(--muted)' }}>— (approbateurs globaux)</span>
                        ) : (
                          <div className="review-tag-list">
                            {scoped.map((a) => {
                              const canRemove = isAdmin || a.sam === mySam;
                              return (
                                <span key={a.d365ApproverId} className="review-tag" style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}>
                                  {a.displayName}
                                  {canRemove && (
                                    <button
                                      type="button"
                                      onClick={() => handleRemove(a.d365ApproverId)}
                                      aria-label={`Retirer ${a.displayName}`}
                                      style={{ display: 'inline-flex', border: 'none', background: 'none', cursor: 'pointer', padding: 0, color: 'inherit' }}
                                    >
                                      ×
                                    </button>
                                  )}
                                </span>
                              );
                            })}
                          </div>
                        )}
                      </td>
                      {isApprover && (
                        <td style={{ padding: '8px 12px', textAlign: 'right', whiteSpace: 'nowrap' }}>
                          <div style={{ display: 'inline-flex', gap: 8 }}>
                            <button
                              type="button"
                              className="review-section__edit"
                              onClick={() => handleSelfAssign(title)}
                              disabled={isMine || assigningTitle === title}
                            >
                              {assigningTitle === title ? 'Assignation…' : 'M\'assigner ce titre'}
                            </button>
                            <button
                              type="button"
                              className="review-section__edit"
                              onClick={() => {
                                const mine = scoped.find((a) => a.sam === mySam);
                                if (mine) handleSelfRemove(title, mine.d365ApproverId);
                              }}
                              disabled={!isMine || removingTitle === title}
                            >
                              {removingTitle === title ? 'Retrait…' : 'Me retirer de ce titre'}
                            </button>
                          </div>
                        </td>
                      )}
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </>
      )}
    </div>
  );
}
