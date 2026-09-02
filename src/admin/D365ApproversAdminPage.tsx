import { useEffect, useRef, useState } from 'react';
import { useApi } from '../api/ApiContext';
import type { AdAccountDto, D365ApproverDto } from '../api/types';

export function D365ApproversAdminPage() {
  const api = useApi();
  const [approvers, setApprovers] = useState<D365ApproverDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  // The account is PICKED from AD rather than typed: the sAMAccountName is the authorization key,
  // and a typo would silently create a row that can never match anyone.
  const [query, setQuery] = useState('');
  const [hits, setHits] = useState<AdAccountDto[]>([]);
  const [isSearching, setIsSearching] = useState(false);
  const [picked, setPicked] = useState<AdAccountDto | null>(null);
  const [positionTitle, setPositionTitle] = useState('');
  const [addError, setAddError] = useState<string | null>(null);
  const [isAdding, setIsAdding] = useState(false);
  const searchSeq = useRef(0);

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

  useEffect(() => {
    if (picked || query.trim().length < 2) {
      setHits([]);
      return;
    }
    const seq = ++searchSeq.current;
    const timer = setTimeout(() => {
      setIsSearching(true);
      api.d365Approvers
        .adSearch(query.trim())
        .then((rows) => {
          if (seq === searchSeq.current) setHits(rows);
        })
        .catch(() => {
          if (seq === searchSeq.current) setHits([]);
        })
        .finally(() => {
          if (seq === searchSeq.current) setIsSearching(false);
        });
    }, 300);
    return () => clearTimeout(timer);
  }, [query, picked, api]);

  const handleAdd = async (ev: React.FormEvent) => {
    ev.preventDefault();
    setAddError(null);

    if (!picked) {
      setAddError('Choisissez un compte dans la liste de résultats.');
      return;
    }

    setIsAdding(true);
    try {
      await api.d365Approvers.add({
        sam: picked.sam,
        displayName: picked.displayName,
        email: picked.email ?? null,
        positionTitle: positionTitle.trim() || null,
      });
      setPicked(null);
      setQuery('');
      setHits([]);
      setPositionTitle('');
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
          <div className="step-panel__title">Approbateurs D365</div>
          <div className="step-panel__subtitle">
            Personnes qui reçoivent un lien pour compléter le formulaire d'accès D365 lorsqu'une demande le
            requiert. Un approbateur <strong>global</strong> (aucun titre de poste) peut agir sur n'importe quelle
            demande. Un approbateur associé à un <strong>titre de poste</strong> précis ne reçoit que les
            demandes visant ce titre exact.
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
                <th style={{ padding: '8px 12px' }}>Nom</th>
                <th style={{ padding: '8px 12px' }}>Compte</th>
                <th style={{ padding: '8px 12px' }}>Courriel</th>
                <th style={{ padding: '8px 12px' }}>Portée</th>
                <th style={{ padding: '8px 12px' }}>Ajouté le</th>
                <th style={{ padding: '8px 12px' }}></th>
              </tr>
            </thead>
            <tbody>
              {approvers.length === 0 && (
                <tr>
                  <td colSpan={6} style={{ padding: '8px 12px', color: 'var(--muted)' }}>
                    Aucun approbateur configuré — les demandes d'accès D365 seront envoyées à l'équipe informatique
                    par courriel jusqu'à ce qu'au moins un approbateur global soit ajouté.
                  </td>
                </tr>
              )}
              {approvers.map((a) => (
                <tr key={a.d365ApproverId} style={{ borderBottom: '1px solid var(--border, #eee)' }}>
                  <td style={{ padding: '8px 12px' }}>{a.displayName}</td>
                  <td style={{ padding: '8px 12px' }}><code>{a.sam}</code></td>
                  <td style={{ padding: '8px 12px' }}>{a.email ?? '—'}</td>
                  <td style={{ padding: '8px 12px' }}>
                    {a.positionTitle ? (
                      a.positionTitle
                    ) : (
                      <span className="review-tag">Global</span>
                    )}
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
            <div className="field">
              <label className="field__label">Rechercher un compte (Active Directory)</label>
              <div className="field__input-wrap">
                <input
                  type="text"
                  value={picked ? `${picked.displayName} (${picked.sam})` : query}
                  onChange={(ev) => {
                    setPicked(null);
                    setQuery(ev.target.value);
                  }}
                  placeholder="Nom ou code d'utilisateur — min. 2 caractères"
                />
              </div>
              {isSearching && <div className="required-note">Recherche…</div>}
              {!picked && hits.length > 0 && (
                <ul style={{ listStyle: 'none', margin: '4px 0 0', padding: 0, border: '1px solid var(--border, #ddd)', maxHeight: 220, overflowY: 'auto' }}>
                  {hits.map((h) => (
                    <li key={h.sam}>
                      <button
                        type="button"
                        onClick={() => { setPicked(h); setHits([]); }}
                        style={{ display: 'block', width: '100%', textAlign: 'left', padding: '6px 10px', border: 'none', background: 'none', cursor: 'pointer' }}
                      >
                        {h.displayName} — <code>{h.sam}</code>
                        {h.email ? ` — ${h.email}` : ''}
                      </button>
                    </li>
                  ))}
                </ul>
              )}
              {!picked && !isSearching && query.trim().length >= 2 && hits.length === 0 && (
                <div className="required-note">Aucun compte trouvé.</div>
              )}
            </div>

            <div className="field">
              <label className="field__label">Titre de poste (Workday) — laisser vide pour un approbateur global</label>
              <div className="field__input-wrap">
                <input
                  type="text"
                  value={positionTitle}
                  onChange={(ev) => setPositionTitle(ev.target.value)}
                  placeholder="ex. : Préposé maintenance — doit correspondre exactement au titre Workday"
                />
              </div>
            </div>

            {addError && <div className="required-note" style={{ color: 'var(--tremblant-red-dark)' }}>{addError}</div>}
            <div>
              <button type="submit" className="btn btn-primary" disabled={isAdding || !picked}>
                Ajouter
              </button>
            </div>
          </form>
        </>
      )}
    </div>
  );
}
