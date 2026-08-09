import { useEffect, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useApi } from '../api/ApiContext';
import { useDebouncedValue } from '../hooks/useDebouncedValue';
import type { D365UserSecurityRoleDto, EmployeeDto } from '../api/types';
import { DiscrepancyTables } from './DiscrepanciesPage';

type Mode = 'corrections' | 'ecarts';

function initials(prenom: string, nom: string) {
  return `${prenom[0] ?? ''}${nom[0] ?? ''}`.toUpperCase();
}

function useEmployeeSearch(query: string) {
  const api = useApi();
  const debounced = useDebouncedValue(query.trim(), 300);
  return useQuery({
    queryKey: ['employees', 'search', debounced],
    queryFn: () => api.employees.search(debounced),
    enabled: debounced.length >= 2,
  });
}

function LinkRow({ row, onLinked, onDeleted }: { row: D365UserSecurityRoleDto; onLinked: () => void; onDeleted: () => void }) {
  const api = useApi();
  const [query, setQuery] = useState('');
  const [error, setError] = useState<string | null>(null);
  const { data: results = [], isFetching } = useEmployeeSearch(query);

  const handleLink = async (emp: EmployeeDto) => {
    setError(null);
    try {
      await api.d365UserSecurityRoles.link(row.id, emp.employeeId);
      setQuery('');
      onLinked();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erreur inconnue');
    }
  };

  const handleDelete = async () => {
    await api.d365UserSecurityRoles.remove(row.id);
    onDeleted();
  };

  return (
    <tr style={{ borderBottom: '1px solid var(--border, #eee)', verticalAlign: 'top' }}>
      <td style={{ padding: '8px 12px' }}>{row.userName}</td>
      <td style={{ padding: '8px 12px' }}>{row.securityRole}</td>
      <td style={{ padding: '8px 12px', minWidth: 320 }}>
        <div className="employee-search">
          <input
            type="text"
            value={query}
            onChange={(ev) => setQuery(ev.target.value)}
            placeholder="Rechercher par numéro, nom ou prénom"
            autoComplete="off"
            style={{ width: '100%' }}
          />
          {query.trim() && (
            <div className="employee-results">
              {isFetching ? (
                <div className="employee-result-empty">Recherche…</div>
              ) : results.length ? (
                results.map((emp) => (
                  <div key={emp.employeeId} className="employee-result-item" onClick={() => handleLink(emp)}>
                    <span className="employee-result-item__avatar">{initials(emp.prenom, emp.nom)}</span>
                    <span>
                      <div className="employee-result-item__name">
                        {emp.prenom} {emp.nom}
                      </div>
                      <div className="employee-result-item__meta">
                        #{emp.employeeId} · {emp.poste} · {emp.departement}
                      </div>
                    </span>
                  </div>
                ))
              ) : (
                <div className="employee-result-empty">Aucun employé ne correspond à cette recherche.</div>
              )}
            </div>
          )}
        </div>
        {error && <div style={{ color: 'var(--danger, #c0392b)', fontSize: 13, marginTop: 4 }}>{error}</div>}
      </td>
      <td style={{ padding: '8px 12px', textAlign: 'right' }}>
        <button type="button" className="review-section__edit" onClick={handleDelete}>
          Supprimer
        </button>
      </td>
    </tr>
  );
}

export function D365UserRolesCorrectionPage() {
  const api = useApi();
  const [mode, setMode] = useState<Mode>('corrections');
  const [rows, setRows] = useState<D365UserSecurityRoleDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  const load = () => {
    setIsLoading(true);
    setLoadError(null);
    api.d365UserSecurityRoles
      .list(true)
      .then(setRows)
      .catch((err) => setLoadError(err instanceof Error ? err.message : 'Erreur inconnue'))
      .finally(() => setIsLoading(false));
  };

  useEffect(load, [api]);

  return (
    <div className="step-panel">
      <div className="step-panel__header">
        <div>
          <div className="step-panel__title">Correction des rôles D365 non liés</div>
          <div className="step-panel__subtitle">
            Lignes importées de Tremblant_D365_Security_Roles.xlsx que l'import n'a pas pu relier automatiquement à un
            employé Workday — recherchez le bon employé pour lier la ligne, ou supprimez-la (ex. compte de service).
          </div>
        </div>
      </div>

      <div style={{ display: 'flex', gap: 8, marginBottom: 16 }}>
        {(['corrections', 'ecarts'] as Mode[]).map((m) => (
          <button
            key={m}
            type="button"
            onClick={() => setMode(m)}
            style={{
              padding: '8px 14px',
              borderRadius: 6,
              border: '1px solid var(--border, #ddd)',
              background: mode === m ? 'var(--brand, #c8102e)' : '#fff',
              color: mode === m ? '#fff' : 'inherit',
              cursor: 'pointer',
              fontWeight: mode === m ? 600 : 400,
            }}
          >
            {m === 'corrections' ? 'Corrections non liées' : 'Écarts / Réconciliation'}
          </button>
        ))}
      </div>

      {mode === 'ecarts' && <DiscrepancyTables />}

      {mode === 'corrections' && isLoading && <div>Chargement…</div>}
      {mode === 'corrections' && loadError && <div className="big-notice">{loadError}</div>}

      {mode === 'corrections' && !isLoading && !loadError && (
        <>
          <div style={{ marginBottom: 16, color: '#666' }}>{rows.length} ligne(s) à corriger</div>
          <table style={{ width: '100%', borderCollapse: 'collapse' }}>
            <thead>
              <tr style={{ textAlign: 'left', borderBottom: '2px solid var(--border, #ddd)' }}>
                <th style={{ padding: '8px 12px' }}>Utilisateur (D365)</th>
                <th style={{ padding: '8px 12px' }}>Rôle</th>
                <th style={{ padding: '8px 12px' }}>Lier à un employé</th>
                <th style={{ padding: '8px 12px' }}></th>
              </tr>
            </thead>
            <tbody>
              {rows.length === 0 && (
                <tr>
                  <td colSpan={4} style={{ padding: '12px', color: '#888' }}>
                    Aucune ligne à corriger — tout est lié.
                  </td>
                </tr>
              )}
              {rows.map((row) => (
                <LinkRow key={row.id} row={row} onLinked={load} onDeleted={load} />
              ))}
            </tbody>
          </table>
        </>
      )}
    </div>
  );
}
