import { useEffect, useState } from 'react';
import { useApi } from '../api/ApiContext';
import type { AppUserDto } from '../api/types';

export function AppUsersAdminPage() {
  const api = useApi();
  const [users, setUsers] = useState<AppUserDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [email, setEmail] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [addError, setAddError] = useState<string | null>(null);
  const [isAdding, setIsAdding] = useState(false);

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

  const handleAdd = async (ev: React.FormEvent) => {
    ev.preventDefault();
    setAddError(null);

    if (!email.trim() || !displayName.trim()) {
      setAddError('Le courriel et le nom sont requis.');
      return;
    }

    setIsAdding(true);
    try {
      await api.appUsers.add({ email: email.trim(), displayName: displayName.trim() });
      setEmail('');
      setDisplayName('');
      load();
    } catch (err) {
      setAddError(err instanceof Error ? err.message : 'Erreur inconnue');
    } finally {
      setIsAdding(false);
    }
  };

  const handleRemove = async (id: number) => {
    await api.appUsers.remove(id);
    load();
  };

  return (
    <div className="step-panel">
      <div className="step-panel__header">
        <div>
          <div className="step-panel__title">Administrateurs — Gabarits des billets</div>
          <div className="step-panel__subtitle">
            Les personnes listées ici peuvent modifier le contenu des billets créés par l'application (menu
            « Gabarits des billets ») et gérer cette liste.
          </div>
        </div>
      </div>

      {isLoading && <div>Chargement…</div>}
      {loadError && <div className="big-notice">{loadError}</div>}

      {!isLoading && !loadError && (
        <>
          <table style={{ width: '100%', borderCollapse: 'collapse', marginBottom: 24 }}>
            <thead>
              <tr style={{ textAlign: 'left', borderBottom: '2px solid var(--border, #ddd)' }}>
                <th style={{ padding: '8px 12px' }}>Nom</th>
                <th style={{ padding: '8px 12px' }}>Courriel</th>
                <th style={{ padding: '8px 12px' }}>Ajouté le</th>
                <th style={{ padding: '8px 12px' }}>Ajouté par</th>
                <th style={{ padding: '8px 12px' }}></th>
              </tr>
            </thead>
            <tbody>
              {users.map((u) => (
                <tr key={u.appUserId} style={{ borderBottom: '1px solid var(--border, #eee)' }}>
                  <td style={{ padding: '8px 12px' }}>{u.displayName}</td>
                  <td style={{ padding: '8px 12px' }}>{u.email}</td>
                  <td style={{ padding: '8px 12px' }}>{new Date(u.createdAt).toLocaleDateString('fr-CA')}</td>
                  <td style={{ padding: '8px 12px' }}>{u.createdByDisplayName ?? '—'}</td>
                  <td style={{ padding: '8px 12px', textAlign: 'right' }}>
                    <button
                      type="button"
                      className="review-section__edit"
                      onClick={() => handleRemove(u.appUserId)}
                      disabled={users.length <= 1}
                      title={users.length <= 1 ? 'Le dernier administrateur ne peut pas être retiré' : undefined}
                    >
                      Retirer
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          <form onSubmit={handleAdd} style={{ display: 'flex', flexDirection: 'column', gap: 12, maxWidth: 420 }}>
            <div className="field">
              <label className="field__label">Nom complet</label>
              <div className="field__input-wrap">
                <input type="text" value={displayName} onChange={(ev) => setDisplayName(ev.target.value)} />
              </div>
            </div>
            <div className="field">
              <label className="field__label">Courriel</label>
              <div className="field__input-wrap">
                <input type="email" value={email} onChange={(ev) => setEmail(ev.target.value)} placeholder="prenom.nom@tremblant.ca" />
              </div>
            </div>
            {addError && <div className="required-note" style={{ color: 'var(--tremblant-red-dark)' }}>{addError}</div>}
            <div>
              <button type="submit" className="btn btn-primary" disabled={isAdding}>
                Ajouter
              </button>
            </div>
          </form>
        </>
      )}
    </div>
  );
}
