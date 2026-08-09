import { useEffect, useState } from 'react';
import { useApi } from '../api/ApiContext';
import { ApiError } from '../api/client';
import type { D365SecurityRoleMappingDto } from '../api/types';

export function D365RolesAdminPage() {
  const api = useApi();
  const [mappings, setMappings] = useState<D365SecurityRoleMappingDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [roleCatalog, setRoleCatalog] = useState<string[]>([]);
  const [jobCode, setJobCode] = useState('');
  const [role, setRole] = useState('');
  const [formError, setFormError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  const load = () => {
    setIsLoading(true);
    setLoadError(null);
    api.d365SecurityRoles
      .list()
      .then(setMappings)
      .catch((err) => setLoadError(err instanceof Error ? err.message : 'Erreur inconnue'))
      .finally(() => setIsLoading(false));
  };

  useEffect(load, [api]);

  useEffect(() => {
    api.d365SecurityRoles.catalog().then((roles) => {
      setRoleCatalog(roles);
      setRole((current) => current || roles[0] || '');
    });
  }, [api]);

  const handleAdd = async (ev: React.FormEvent) => {
    ev.preventDefault();
    if (!jobCode.trim()) {
      setFormError('Le code d\'emploi est requis.');
      return;
    }

    setIsSaving(true);
    setFormError(null);
    try {
      await api.d365SecurityRoles.create({ jobCode: jobCode.trim(), role });
      setJobCode('');
      load();
    } catch (err) {
      if (err instanceof ApiError && err.status === 409) {
        setFormError('Cette combinaison code d\'emploi / rôle existe déjà.');
      } else {
        setFormError(err instanceof Error ? err.message : 'Erreur inconnue');
      }
    } finally {
      setIsSaving(false);
    }
  };

  const handleDelete = async (id: number) => {
    await api.d365SecurityRoles.remove(id);
    load();
  };

  return (
    <div className="step-panel">
      <div className="step-panel__header">
        <div>
          <div className="step-panel__title">Rôles de sécurité D365 par code d'emploi</div>
          <div className="step-panel__subtitle">
            Détermine quels rôles D365 sont demandés dans le billet TDX lorsqu'un employé nécessite un accès D365 —
            un code d'emploi peut avoir plusieurs rôles.
          </div>
        </div>
      </div>

      <form onSubmit={handleAdd} style={{ display: 'flex', gap: 12, alignItems: 'flex-end', flexWrap: 'wrap', marginBottom: 24 }}>
        <div className="field" style={{ marginBottom: 0 }}>
          <label className="field__label">Code d'emploi</label>
          <div className="field__input-wrap">
            <input
              type="text"
              value={jobCode}
              onChange={(ev) => setJobCode(ev.target.value)}
              placeholder="Ex. 2253"
              style={{ width: 160 }}
            />
          </div>
        </div>

        <div className="field" style={{ marginBottom: 0 }}>
          <label className="field__label">Rôle D365</label>
          <div className="field__input-wrap">
            <select value={role} onChange={(ev) => setRole(ev.target.value)} style={{ minWidth: 320 }}>
              {roleCatalog.map((r) => (
                <option key={r} value={r}>
                  {r}
                </option>
              ))}
            </select>
          </div>
        </div>

        <button type="submit" className="btn btn--primary" disabled={isSaving}>
          Ajouter
        </button>
      </form>

      {formError && <div className="big-notice">{formError}</div>}

      {isLoading && <div>Chargement…</div>}
      {loadError && <div className="big-notice">{loadError}</div>}

      {!isLoading && !loadError && (
        <table style={{ width: '100%', borderCollapse: 'collapse' }}>
          <thead>
            <tr style={{ textAlign: 'left', borderBottom: '2px solid var(--border, #ddd)' }}>
              <th style={{ padding: '8px 12px' }}>Code d'emploi</th>
              <th style={{ padding: '8px 12px' }}>Titre du poste</th>
              <th style={{ padding: '8px 12px' }}>Rôle D365</th>
              <th style={{ padding: '8px 12px' }}></th>
            </tr>
          </thead>
          <tbody>
            {mappings.length === 0 && (
              <tr>
                <td colSpan={4} style={{ padding: '12px', color: '#888' }}>
                  Aucun mappage — ajoutez-en un ci-dessus.
                </td>
              </tr>
            )}
            {mappings.map((m) => (
              <tr key={m.id} style={{ borderBottom: '1px solid var(--border, #eee)' }}>
                <td style={{ padding: '8px 12px' }}>{m.jobCode}</td>
                <td style={{ padding: '8px 12px', color: m.positionTitle ? undefined : '#888' }}>
                  {m.positionTitle ?? '—'}
                </td>
                <td style={{ padding: '8px 12px' }}>{m.role}</td>
                <td style={{ padding: '8px 12px', textAlign: 'right' }}>
                  <button type="button" className="review-section__edit" onClick={() => handleDelete(m.id)}>
                    Supprimer
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
