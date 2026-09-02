import { useEffect, useMemo, useState } from 'react';
import { useApi } from '../api/ApiContext';
import type { D365ApproverDto, MeDto } from '../api/types';
import { usePicker, PickerField } from '../components/AdPicker';

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
 * Position Title at all) — see Approbateurs D365 for managing those. Multiple approvers per title
 * is intentional, same as everywhere else in this app: any one matched approver acting is enough.
 *
 * The assign controls sit ABOVE the table on purpose — there are 300+ titles, so anything placed
 * below the table was effectively invisible without scrolling past all of them (a real report: an
 * admin scrolled the whole table and never found the self-assign button). The table itself is
 * capped to a scrollable panel with a sticky header and a text filter, so it doesn't dominate the
 * page either.
 *
 * Two INDEPENDENT assign actions, matching the backend's two tiers exactly:
 *  - "M'assigner à ce titre" — visible to ANY D365Approver, admin or not. One click, no picker:
 *    it always claims the title under the CALLER's own identity. An admin who is also an approver
 *    (the common case) needs this too; without it they had no way to add themselves short of
 *    searching AD for their own name in the picker below.
 *  - "Assigner un autre compte" — Admin only, full AD picker, for assigning anyone else. */
export function D365PositionTitleAssignmentsPage({ me }: { me: MeDto }) {
  const api = useApi();
  const isAdmin = me.isTicketTemplateAdmin;
  const isApprover = me.isD365Approver;
  const mySam = bareSam(me.objectId);

  const [titles, setTitles] = useState<string[]>([]);
  const [approvers, setApprovers] = useState<D365ApproverDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [filter, setFilter] = useState('');

  const [selectedTitle, setSelectedTitle] = useState('');
  const [selfError, setSelfError] = useState<string | null>(null);
  const [isSelfAssigning, setIsSelfAssigning] = useState(false);
  const [otherError, setOtherError] = useState<string | null>(null);
  const [isAssigningOther, setIsAssigningOther] = useState(false);
  const picker = usePicker((q) => api.d365Approvers.adSearch(q));

  const load = () => {
    setIsLoading(true);
    setLoadError(null);
    Promise.all([api.d365Approvers.positionTitles(), api.d365Approvers.list()])
      .then(([t, a]) => {
        setTitles(t);
        setApprovers(a);
        setSelectedTitle((current) => current || t[0] || '');
      })
      .catch((err) => setLoadError(err instanceof Error ? err.message : 'Erreur inconnue'))
      .finally(() => setIsLoading(false));
  };

  useEffect(load, [api]);

  const globalCount = approvers.filter((a) => !a.positionTitle).length;
  const alreadyMine = approvers.some((a) => a.positionTitle === selectedTitle && a.sam === mySam);
  const filteredTitles = useMemo(() => {
    const q = filter.trim().toLowerCase();
    return q ? titles.filter((t) => t.toLowerCase().includes(q)) : titles;
  }, [titles, filter]);

  const handleSelfAssign = async () => {
    setSelfError(null);
    if (!selectedTitle) {
      setSelfError('Choisissez un titre de poste.');
      return;
    }
    setIsSelfAssigning(true);
    try {
      await api.d365Approvers.add({
        sam: me.objectId,
        displayName: me.displayName,
        email: me.email ?? null,
        positionTitle: selectedTitle,
      });
      load();
    } catch (err) {
      setSelfError(err instanceof Error ? err.message : 'Erreur inconnue');
    } finally {
      setIsSelfAssigning(false);
    }
  };

  const handleAssignOther = async (ev: React.FormEvent) => {
    ev.preventDefault();
    setOtherError(null);

    if (!selectedTitle) {
      setOtherError('Choisissez un titre de poste.');
      return;
    }
    if (!picker.picked) {
      setOtherError('Choisissez un compte dans la liste de résultats.');
      return;
    }

    setIsAssigningOther(true);
    try {
      await api.d365Approvers.add({
        sam: picker.picked.sam,
        displayName: picker.picked.displayName,
        email: picker.picked.email ?? null,
        positionTitle: selectedTitle,
      });
      picker.reset();
      load();
    } catch (err) {
      setOtherError(err instanceof Error ? err.message : 'Erreur inconnue');
    } finally {
      setIsAssigningOther(false);
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

  return (
    <div className="step-panel">
      <div className="step-panel__header">
        <div>
          <div className="step-panel__title">Titres de poste</div>
          <div className="step-panel__subtitle">
            Un titre de poste sans approbateur assigné dans le tableau ci-dessous utilise les approbateurs{' '}
            <strong>globaux</strong> ({globalCount === 0 ? 'aucun configuré pour l\'instant' : `${globalCount} présentement`}
            {' '}— voir Approbateurs D365).
          </div>
        </div>
      </div>

      {isLoading && <div>Chargement…</div>}
      {loadError && <div className="big-notice">{loadError}</div>}

      {!isLoading && (
        <>
          <div className="field-section-title">Assigner un titre de poste</div>
          <div className="field" style={{ maxWidth: 520 }}>
            <label className="field__label">Titre de poste ({titles.length})</label>
            <div className="field__input-wrap">
              <select value={selectedTitle} onChange={(ev) => setSelectedTitle(ev.target.value)}>
                {titles.map((title) => (
                  <option key={title} value={title}>{title}</option>
                ))}
              </select>
            </div>
          </div>

          {isApprover && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 8, maxWidth: 520, marginTop: 12 }}>
              <div style={{ fontWeight: 600, fontSize: 13 }}>Vous êtes un approbateur D365</div>
              {alreadyMine ? (
                <div className="required-note">Vous êtes déjà assigné à ce titre.</div>
              ) : (
                <div>
                  <button
                    type="button"
                    className="btn btn-secondary"
                    onClick={handleSelfAssign}
                    disabled={isSelfAssigning || !selectedTitle}
                  >
                    {isSelfAssigning ? 'Assignation…' : 'M\'assigner à ce titre'}
                  </button>
                </div>
              )}
              {selfError && <div className="required-note" style={{ color: 'var(--tremblant-red-dark)' }}>{selfError}</div>}
            </div>
          )}

          {isAdmin && (
            <form onSubmit={handleAssignOther} style={{ display: 'flex', flexDirection: 'column', gap: 12, maxWidth: 520, marginTop: 20 }}>
              <div style={{ fontWeight: 600, fontSize: 13 }}>Ou assigner un autre compte (admin)</div>
              <PickerField picker={picker} />
              {otherError && <div className="required-note" style={{ color: 'var(--tremblant-red-dark)' }}>{otherError}</div>}
              <div>
                <button type="submit" className="btn btn-primary" disabled={isAssigningOther || !selectedTitle || !picker.picked}>
                  Assigner
                </button>
              </div>
            </form>
          )}

          <div className="field-section-title" style={{ marginTop: 24 }}>Tous les titres de poste</div>
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

          <div style={{ overflow: 'auto', maxHeight: 420, border: '1px solid var(--border, #ddd)' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse' }}>
              <thead>
                <tr style={{ textAlign: 'left', borderBottom: '2px solid var(--border, #ddd)' }}>
                  <th style={{ padding: '8px 12px', position: 'sticky', top: 0, background: 'var(--bg-card, #fff)' }}>Titre de poste</th>
                  <th style={{ padding: '8px 12px', position: 'sticky', top: 0, background: 'var(--bg-card, #fff)' }}>Approbateur(s) assigné(s)</th>
                </tr>
              </thead>
              <tbody>
                {filteredTitles.length === 0 && (
                  <tr>
                    <td colSpan={2} style={{ padding: '8px 12px', color: 'var(--muted)' }}>
                      {titles.length === 0 ? 'Aucun titre de poste trouvé.' : 'Aucun titre ne correspond au filtre.'}
                    </td>
                  </tr>
                )}
                {filteredTitles.map((title) => {
                  const scoped = approvers.filter((a) => a.positionTitle === title);
                  return (
                    <tr key={title} style={{ borderBottom: '1px solid var(--border, #eee)' }}>
                      <td style={{ padding: '8px 12px' }}>{title}</td>
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
