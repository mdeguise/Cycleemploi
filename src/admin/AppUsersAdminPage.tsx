import { useEffect, useRef, useState } from 'react';
import { useApi } from '../api/ApiContext';
import type { AdAccountDto, AppUserDto, AppUserRole } from '../api/types';

const ROLE_LABELS: Record<AppUserRole, string> = {
  Admin: 'Administrateur',
  Lecteur: 'Lecteur (consultation seulement)',
};

export function AppUsersAdminPage() {
  const api = useApi();
  const [users, setUsers] = useState<AppUserDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  // The account is PICKED from AD rather than typed: the sAMAccountName is the authorization key,
  // and a typo would silently create a row that can never match anyone.
  const [query, setQuery] = useState('');
  const [hits, setHits] = useState<AdAccountDto[]>([]);
  const [isSearching, setIsSearching] = useState(false);
  const [picked, setPicked] = useState<AdAccountDto | null>(null);
  const [role, setRole] = useState<AppUserRole>('Lecteur');
  const [addError, setAddError] = useState<string | null>(null);
  const [isAdding, setIsAdding] = useState(false);
  const searchSeq = useRef(0);

  const load = () => {
    setIsLoading(true);
    setLoadError(null);
    api.appUsers
      .list()
      .then(setUsers)
      .catch((err) => setLoadError(err instanceof Error ? err.message : 'Erreur inconnue'))
      .finally(() => setIsLoading(false));
  };

  useEffect(load, [api]);

  // Debounced AD lookup. searchSeq guards against a slow earlier request landing after a faster
  // later one and overwriting the newer results.
  useEffect(() => {
    if (picked || query.trim().length < 2) {
      setHits([]);
      return;
    }
    const seq = ++searchSeq.current;
    const timer = setTimeout(() => {
      setIsSearching(true);
      api.appUsers
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

  const adminCount = users.filter((u) => u.role === 'Admin').length;

  const handleAdd = async (ev: React.FormEvent) => {
    ev.preventDefault();
    setAddError(null);

    if (!picked) {
      setAddError('Choisissez un compte dans la liste de résultats.');
      return;
    }

    setIsAdding(true);
    try {
      await api.appUsers.add({
        sam: picked.sam,
        displayName: picked.displayName,
        email: picked.email ?? null,
        role,
      });
      setPicked(null);
      setQuery('');
      setHits([]);
      setRole('Lecteur');
      load();
    } catch (err) {
      setAddError(err instanceof Error ? err.message : 'Erreur inconnue');
    } finally {
      setIsAdding(false);
    }
  };

  const handleRoleChange = async (id: number, next: AppUserRole) => {
    setLoadError(null);
    try {
      await api.appUsers.updateRole(id, next);
      load();
    } catch (err) {
      setLoadError(err instanceof Error ? err.message : 'Erreur inconnue');
    }
  };

  const handleRemove = async (id: number) => {
    setLoadError(null);
    try {
      await api.appUsers.remove(id);
      load();
    } catch (err) {
      setLoadError(err instanceof Error ? err.message : 'Erreur inconnue');
    }
  };

  return (
    <div className="step-panel">
      <div className="step-panel__header">
        <div>
          <div className="step-panel__title">Administrateurs</div>
          <div className="step-panel__subtitle">
            Contrôle l'accès à la section Administration. Un <strong>administrateur</strong> peut tout faire :
            consulter les demandes, relancer la création d'un billet, modifier les gabarits et gérer cette liste.
            Un <strong>lecteur</strong> peut seulement consulter les demandes et leurs billets.
            L'accès est associé au compte Windows (sAMAccountName), pas au courriel — un compte
            administrateur (<code>_adm</code>) est donc une identité distincte et doit être ajouté séparément.
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
                <th style={{ padding: '8px 12px' }}>Rôle</th>
                <th style={{ padding: '8px 12px' }}>Ajouté le</th>
                <th style={{ padding: '8px 12px' }}>Ajouté par</th>
                <th style={{ padding: '8px 12px' }}></th>
              </tr>
            </thead>
            <tbody>
              {users.map((u) => {
                const isLastAdmin = u.role === 'Admin' && adminCount <= 1;
                return (
                  <tr key={u.appUserId} style={{ borderBottom: '1px solid var(--border, #eee)' }}>
                    <td style={{ padding: '8px 12px' }}>{u.displayName}</td>
                    <td style={{ padding: '8px 12px' }}><code>{u.sam}</code></td>
                    <td style={{ padding: '8px 12px' }}>{u.email ?? '—'}</td>
                    <td style={{ padding: '8px 12px' }}>
                      <select
                        value={u.role}
                        onChange={(ev) => handleRoleChange(u.appUserId, ev.target.value as AppUserRole)}
                        disabled={isLastAdmin}
                        title={isLastAdmin ? 'Il doit rester au moins un administrateur' : undefined}
                      >
                        <option value="Admin">{ROLE_LABELS.Admin}</option>
                        <option value="Lecteur">{ROLE_LABELS.Lecteur}</option>
                      </select>
                    </td>
                    <td style={{ padding: '8px 12px' }}>{new Date(u.createdAt).toLocaleDateString('fr-CA')}</td>
                    <td style={{ padding: '8px 12px' }}>{u.createdByDisplayName ?? '—'}</td>
                    <td style={{ padding: '8px 12px', textAlign: 'right' }}>
                      <button
                        type="button"
                        className="review-section__edit"
                        onClick={() => handleRemove(u.appUserId)}
                        disabled={isLastAdmin}
                        title={isLastAdmin ? 'Le dernier administrateur ne peut pas être retiré' : undefined}
                      >
                        Retirer
                      </button>
                    </td>
                  </tr>
                );
              })}
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
              <label className="field__label">Rôle</label>
              <div className="field__input-wrap">
                <select value={role} onChange={(ev) => setRole(ev.target.value as AppUserRole)}>
                  <option value="Lecteur">{ROLE_LABELS.Lecteur}</option>
                  <option value="Admin">{ROLE_LABELS.Admin}</option>
                </select>
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
