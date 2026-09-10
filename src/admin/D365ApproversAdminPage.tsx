import { useEffect, useState } from 'react';
import { useApi } from '../api/ApiContext';
import type { D365ApprovalRole, D365ApproverDto, D365ViewerDto } from '../api/types';
import { usePicker, PickerField } from '../components/AdPicker';

const APPROVAL_ROLE_LABELS: Record<string, string> = {
  Dynaway: 'Dynaway (étape unique)',
  Stage1: 'Étape 1',
  Stage2: 'Étape 2',
};
const APPROVAL_ROLES: D365ApprovalRole[] = ['Dynaway', 'Stage1', 'Stage2'];

function ApproversSection() {
  const api = useApi();
  const [approvers, setApprovers] = useState<D365ApproverDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [approvalRole, setApprovalRole] = useState<D365ApprovalRole>('Dynaway');
  const [addError, setAddError] = useState<string | null>(null);
  const [isAdding, setIsAdding] = useState(false);
  const picker = usePicker((q) => api.d365Approvers.adSearch(q));

  const load = () => {
    setIsLoading(true);
    setLoadError(null);
    api.d365Approvers
      .list()
      .then(setApprovers)
      .catch((err) => setLoadError(err instanceof Error ? err.message : 'Erreur inconnue'))
      .finally(() => setIsLoading(false));
  };

  useEffect(load, [api]);

  const handleAdd = async (ev: React.FormEvent) => {
    ev.preventDefault();
    setAddError(null);
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
        approvalRole,
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
    <>
      <div className="field-section-title">Approbateurs D365</div>
      <div className="step-panel__subtitle" style={{ marginTop: -4 }}>
        Personnes qui reçoivent un lien pour agir sur une demande d'accès D365. Une demande qui coche
        « Besoin de gestion des actifs (Asset Management) avec Dynaway » va directement à <strong>Dynaway</strong> (approbation
        unique, billet TDX créé immédiatement). Toute autre demande passe d'abord par <strong>Étape 1</strong> (remplit le
        formulaire), puis par <strong>Étape 2</strong> (confirmation finale, sans nouvelle saisie) avant la création du billet
        TDX. Plusieurs personnes peuvent partager le même rôle — n'importe laquelle peut agir pour cette étape.
      </div>

      {isLoading && <div>Chargement…</div>}
      {loadError && <div className="big-notice">{loadError}</div>}

      {!isLoading && (
        <>
          <table style={{ width: '100%', borderCollapse: 'collapse', marginBottom: 24 }}>
            <thead>
              <tr style={{ textAlign: 'left', borderBottom: '2px solid var(--border, #ddd)' }}>
                <th style={{ padding: '8px 12px' }}>Nom</th>
                <th style={{ padding: '8px 12px' }}>Compte</th>
                <th style={{ padding: '8px 12px' }}>Courriel</th>
                <th style={{ padding: '8px 12px' }}>Rôle</th>
                <th style={{ padding: '8px 12px' }}>Ajouté le</th>
                <th style={{ padding: '8px 12px' }}></th>
              </tr>
            </thead>
            <tbody>
              {approvers.length === 0 && (
                <tr>
                  <td colSpan={6} style={{ padding: '8px 12px', color: 'var(--muted)' }}>
                    Aucun approbateur configuré — les demandes d'accès D365 seront envoyées à l'équipe informatique
                    par courriel jusqu'à ce qu'un approbateur soit ajouté pour chaque rôle.
                  </td>
                </tr>
              )}
              {approvers.map((a) => (
                <tr key={a.d365ApproverId} style={{ borderBottom: '1px solid var(--border, #eee)' }}>
                  <td style={{ padding: '8px 12px' }}>{a.displayName}</td>
                  <td style={{ padding: '8px 12px' }}><code>{a.sam}</code></td>
                  <td style={{ padding: '8px 12px' }}>{a.email ?? '—'}</td>
                  <td style={{ padding: '8px 12px' }}>
                    <span className="review-tag">{APPROVAL_ROLE_LABELS[a.approvalRole] ?? a.approvalRole}</span>
                  </td>
                  <td style={{ padding: '8px 12px' }}>{new Date(a.createdAt).toLocaleDateString('fr-CA')}</td>
                  <td style={{ padding: '8px 12px', textAlign: 'right' }}>
                    <button type="button" className="review-section__edit" onClick={() => handleRemove(a.d365ApproverId)}>
                      Retirer
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          <form onSubmit={handleAdd} style={{ display: 'flex', flexDirection: 'column', gap: 12, maxWidth: 520 }}>
            <PickerField picker={picker} />
            <div className="field">
              <label className="field__label">Rôle</label>
              <div className="field__input-wrap">
                <select value={approvalRole} onChange={(ev) => setApprovalRole(ev.target.value as D365ApprovalRole)}>
                  {APPROVAL_ROLES.map((r) => (
                    <option key={r} value={r}>{APPROVAL_ROLE_LABELS[r]}</option>
                  ))}
                </select>
              </div>
            </div>
            {addError && <div className="required-note" style={{ color: 'var(--tremblant-red-dark)' }}>{addError}</div>}
            <div>
              <button type="submit" className="btn btn-primary" disabled={isAdding || !picker.picked}>
                Ajouter
              </button>
            </div>
          </form>
        </>
      )}
    </>
  );
}

function ViewersSection() {
  const api = useApi();
  const [viewers, setViewers] = useState<D365ViewerDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [addError, setAddError] = useState<string | null>(null);
  const [isAdding, setIsAdding] = useState(false);
  const picker = usePicker((q) => api.d365Viewers.adSearch(q));

  const load = () => {
    setIsLoading(true);
    setLoadError(null);
    api.d365Viewers
      .list()
      .then(setViewers)
      .catch((err) => setLoadError(err instanceof Error ? err.message : 'Erreur inconnue'))
      .finally(() => setIsLoading(false));
  };

  useEffect(load, [api]);

  const handleAdd = async (ev: React.FormEvent) => {
    ev.preventDefault();
    setAddError(null);
    if (!picker.picked) {
      setAddError('Choisissez un compte dans la liste de résultats.');
      return;
    }
    setIsAdding(true);
    try {
      await api.d365Viewers.add({
        sam: picker.picked.sam,
        displayName: picker.picked.displayName,
        email: picker.picked.email ?? null,
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
      await api.d365Viewers.remove(id);
      load();
    } catch (err) {
      setLoadError(err instanceof Error ? err.message : 'Erreur inconnue');
    }
  };

  return (
    <>
      <div className="field-section-title">Personnel TI (lecture seule)</div>
      <div className="step-panel__subtitle" style={{ marginTop: -4 }}>
        Personnes qui peuvent consulter la liste des demandes d'accès D365 et leur statut, sans pouvoir remplir ni
        envoyer un formulaire — utile pour le suivi par l'équipe informatique.
      </div>

      {isLoading && <div>Chargement…</div>}
      {loadError && <div className="big-notice">{loadError}</div>}

      {!isLoading && (
        <>
          <table style={{ width: '100%', borderCollapse: 'collapse', marginBottom: 24 }}>
            <thead>
              <tr style={{ textAlign: 'left', borderBottom: '2px solid var(--border, #ddd)' }}>
                <th style={{ padding: '8px 12px' }}>Nom</th>
                <th style={{ padding: '8px 12px' }}>Compte</th>
                <th style={{ padding: '8px 12px' }}>Courriel</th>
                <th style={{ padding: '8px 12px' }}>Ajouté le</th>
                <th style={{ padding: '8px 12px' }}></th>
              </tr>
            </thead>
            <tbody>
              {viewers.length === 0 && (
                <tr>
                  <td colSpan={5} style={{ padding: '8px 12px', color: 'var(--muted)' }}>
                    Aucune personne configurée pour la consultation seule.
                  </td>
                </tr>
              )}
              {viewers.map((v) => (
                <tr key={v.d365ViewerId} style={{ borderBottom: '1px solid var(--border, #eee)' }}>
                  <td style={{ padding: '8px 12px' }}>{v.displayName}</td>
                  <td style={{ padding: '8px 12px' }}><code>{v.sam}</code></td>
                  <td style={{ padding: '8px 12px' }}>{v.email ?? '—'}</td>
                  <td style={{ padding: '8px 12px' }}>{new Date(v.createdAt).toLocaleDateString('fr-CA')}</td>
                  <td style={{ padding: '8px 12px', textAlign: 'right' }}>
                    <button type="button" className="review-section__edit" onClick={() => handleRemove(v.d365ViewerId)}>
                      Retirer
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          <form onSubmit={handleAdd} style={{ display: 'flex', flexDirection: 'column', gap: 12, maxWidth: 520 }}>
            <PickerField picker={picker} />
            {addError && <div className="required-note" style={{ color: 'var(--tremblant-red-dark)' }}>{addError}</div>}
            <div>
              <button type="submit" className="btn btn-primary" disabled={isAdding || !picker.picked}>
                Ajouter
              </button>
            </div>
          </form>
        </>
      )}
    </>
  );
}

export function D365ApproversAdminPage() {
  return (
    <div className="step-panel">
      <div className="step-panel__header">
        <div>
          <div className="step-panel__title">Approbateurs D365</div>
          <div className="step-panel__subtitle">
            Qui peut agir sur une demande d'accès D365, et qui peut seulement en suivre le statut.
          </div>
        </div>
      </div>

      <ApproversSection />
      <ViewersSection />
    </div>
  );
}
