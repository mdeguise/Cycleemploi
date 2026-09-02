import { useEffect, useState } from 'react';
import { useApi } from '../api/ApiContext';
import type { D365ApproverDto } from '../api/types';
import { usePicker, PickerField } from '../components/AdPicker';

/** One row per distinct Workday Position_Title, showing which D365Approver(s) are scoped
 * specifically to it. A title with none shown falls back to whichever approvers are GLOBAL (no
 * Position Title at all) — see Approbateurs D365 for managing those. Multiple approvers per title
 * is intentional, same as everywhere else in this app: any one matched approver acting is enough. */
export function D365PositionTitleAssignmentsPage() {
  const api = useApi();
  const [titles, setTitles] = useState<string[]>([]);
  const [approvers, setApprovers] = useState<D365ApproverDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [selectedTitle, setSelectedTitle] = useState('');
  const [addError, setAddError] = useState<string | null>(null);
  const [isAdding, setIsAdding] = useState(false);
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

  const handleAssign = async (ev: React.FormEvent) => {
    ev.preventDefault();
    setAddError(null);

    if (!selectedTitle) {
      setAddError('Choisissez un titre de poste.');
      return;
    }
    if (!picker.picked) {
      setAddError('Choisissez un compte dans la liste de résultats.');
      return;
    }

    setIsAdding(true);
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
      setAddError(err instanceof Error ? err.message : 'Erreur inconnue');
    } finally {
      setIsAdding(false);
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
            Un titre de poste sans approbateur assigné ci-dessous utilise les approbateurs{' '}
            <strong>globaux</strong> ({globalCount === 0 ? 'aucun configuré pour l\'instant' : `${globalCount} présentement`}
            {' '}— voir Approbateurs D365).
          </div>
        </div>
      </div>

      {isLoading && <div>Chargement…</div>}
      {loadError && <div className="big-notice">{loadError}</div>}

      {!isLoading && (
        <>
          <table style={{ width: '100%', borderCollapse: 'collapse', marginBottom: 24 }}>
            <thead>
              <tr style={{ textAlign: 'left', borderBottom: '2px solid var(--border, #ddd)' }}>
                <th style={{ padding: '8px 12px' }}>Titre de poste</th>
                <th style={{ padding: '8px 12px' }}>Approbateur(s) assigné(s)</th>
              </tr>
            </thead>
            <tbody>
              {titles.length === 0 && (
                <tr>
                  <td colSpan={2} style={{ padding: '8px 12px', color: 'var(--muted)' }}>Aucun titre de poste trouvé.</td>
                </tr>
              )}
              {titles.map((title) => {
                const scoped = approvers.filter((a) => a.positionTitle === title);
                return (
                  <tr key={title} style={{ borderBottom: '1px solid var(--border, #eee)' }}>
                    <td style={{ padding: '8px 12px' }}>{title}</td>
                    <td style={{ padding: '8px 12px' }}>
                      {scoped.length === 0 ? (
                        <span style={{ color: 'var(--muted)' }}>— (approbateurs globaux)</span>
                      ) : (
                        <div className="review-tag-list">
                          {scoped.map((a) => (
                            <span key={a.d365ApproverId} className="review-tag" style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}>
                              {a.displayName}
                              <button
                                type="button"
                                onClick={() => handleRemove(a.d365ApproverId)}
                                aria-label={`Retirer ${a.displayName}`}
                                style={{ display: 'inline-flex', border: 'none', background: 'none', cursor: 'pointer', padding: 0, color: 'inherit' }}
                              >
                                ×
                              </button>
                            </span>
                          ))}
                        </div>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>

          <div className="field-section-title">Assigner un approbateur</div>
          <form onSubmit={handleAssign} style={{ display: 'flex', flexDirection: 'column', gap: 12, maxWidth: 520 }}>
            <div className="field">
              <label className="field__label">Titre de poste</label>
              <div className="field__input-wrap">
                <select value={selectedTitle} onChange={(ev) => setSelectedTitle(ev.target.value)}>
                  {titles.map((title) => (
                    <option key={title} value={title}>{title}</option>
                  ))}
                </select>
              </div>
            </div>

            <PickerField picker={picker} />

            {addError && <div className="required-note" style={{ color: 'var(--tremblant-red-dark)' }}>{addError}</div>}
            <div>
              <button type="submit" className="btn btn-primary" disabled={isAdding || !picker.picked || !selectedTitle}>
                Assigner
              </button>
            </div>
          </form>
        </>
      )}
    </div>
  );
}
