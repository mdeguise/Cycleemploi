import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useApi } from '../api/ApiContext';
import type { DiscrepanciesDto } from '../api/types';

type TabKey = 'tremblantDynaway' | 'noActiveAd' | 'dynawayNoD365Role' | 'd365InactiveWorkday';

const TABS: { key: TabKey; label: string; countKey: keyof DiscrepanciesDto['summary'] }[] = [
  { key: 'tremblantDynaway', label: '1 — Dynaway Tremblant', countKey: 'tremblantDynawayCount' },
  { key: 'noActiveAd', label: '2a — Sans compte AD actif', countKey: 'noActiveAdCount' },
  { key: 'dynawayNoD365Role', label: '2b — Dynaway sans rôle D365', countKey: 'dynawayNoD365RoleCount' },
  { key: 'd365InactiveWorkday', label: '2c — D365 / Workday inactif', countKey: 'd365InactiveWorkdayCount' },
];

const COLUMNS: Record<TabKey, { key: string; label: string }[]> = {
  tremblantDynaway: [
    { key: 'name', label: 'Nom (AD)' },
    { key: 'login', label: 'Login AD' },
    { key: 'adEnabled', label: 'Compte AD actif' },
    { key: 'hasD365Role', label: 'A un rôle D365' },
    { key: 'd365RoleCount', label: '# rôles D365' },
  ],
  noActiveAd: [
    { key: 'source', label: 'Source' },
    { key: 'name', label: 'Nom' },
    { key: 'login', label: 'Login AD' },
    { key: 'status', label: 'Statut AD' },
  ],
  dynawayNoD365Role: [
    { key: 'name', label: 'Nom (AD)' },
    { key: 'login', label: 'Login AD' },
    { key: 'adEnabled', label: 'Compte AD actif' },
  ],
  d365InactiveWorkday: [
    { key: 'userName', label: 'Utilisateur D365' },
    { key: 'employeeId', label: 'Employee ID' },
    { key: 'workdayStatus', label: 'Statut Workday' },
    { key: 'd365RoleCount', label: '# rôles' },
    { key: 'roles', label: 'Rôles' },
  ],
};

function cellText(v: unknown): string {
  if (v === true) return 'Oui';
  if (v === false) return 'Non';
  if (v === null || v === undefined) return '';
  return String(v);
}

function toCsv(cols: { key: string; label: string }[], rows: Record<string, unknown>[]): string {
  const esc = (s: string) => `"${s.replace(/"/g, '""')}"`;
  const head = cols.map((c) => esc(c.label)).join(',');
  const body = rows.map((r) => cols.map((c) => esc(cellText(r[c.key]))).join(',')).join('\r\n');
  return '﻿' + head + '\r\n' + body;
}

/** The four reconciliation tables, embedded inside the "Correction des rôles D365" admin page as a
 * second mode alongside the unmatched-rows correction UI. Fetches its own data. */
export function DiscrepancyTables() {
  const api = useApi();
  const [tab, setTab] = useState<TabKey>('tremblantDynaway');

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['discrepancies'],
    queryFn: () => api.discrepancies.get(),
  });

  const rows = useMemo(
    () => ((data ? (data[tab] as unknown as Record<string, unknown>[]) : []) ?? []),
    [data, tab],
  );
  const cols = COLUMNS[tab];

  const download = () => {
    const blob = new Blob([toCsv(cols, rows)], { type: 'text/csv;charset=utf-8' });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = `ecarts_${tab}.csv`;
    document.body.appendChild(a);
    a.click();
    a.remove();
  };

  return (
    <div>
      <div className="step-panel__subtitle" style={{ marginBottom: 16 }}>
        Croise les rôles de sécurité D365, les licences Dynaway, Active Directory (comptes Tremblant) et le statut
        démographique Workday. Données de référence en lecture seule.
      </div>

      {isLoading && <div>Chargement…</div>}
      {isError && (
        <div className="big-notice">
          {error instanceof Error ? error.message : 'Erreur inconnue'} — réservé au groupe TRM-CYCLEEMPLOI-D365-ADMIN.
        </div>
      )}

      {data && (
        <>
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginBottom: 16 }}>
            {TABS.map((t) => {
              const count = data.summary[t.countKey] as number;
              const active = t.key === tab;
              return (
                <button
                  key={t.key}
                  type="button"
                  onClick={() => setTab(t.key)}
                  style={{
                    padding: '8px 14px',
                    borderRadius: 6,
                    border: '1px solid var(--border, #ddd)',
                    background: active ? 'var(--brand, #c8102e)' : '#fff',
                    color: active ? '#fff' : 'inherit',
                    cursor: 'pointer',
                    fontWeight: active ? 600 : 400,
                  }}
                >
                  {t.label} <span style={{ opacity: 0.8 }}>({count})</span>
                </button>
              );
            })}
          </div>

          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 8 }}>
            <div style={{ color: '#666' }}>
              {rows.length} ligne(s) · Dynaway total : {data.summary.dynawayLicensesTotal}
            </div>
            <button type="button" className="review-section__edit" onClick={download} disabled={!rows.length}>
              Exporter en CSV
            </button>
          </div>

          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse' }}>
              <thead>
                <tr style={{ textAlign: 'left', borderBottom: '2px solid var(--border, #ddd)' }}>
                  {cols.map((c) => (
                    <th key={c.key} style={{ padding: '8px 12px', whiteSpace: 'nowrap' }}>
                      {c.label}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {rows.length === 0 && (
                  <tr>
                    <td colSpan={cols.length} style={{ padding: 12, color: '#888' }}>
                      Aucun écart dans cette catégorie.
                    </td>
                  </tr>
                )}
                {rows.map((r, i) => (
                  <tr key={i} style={{ borderBottom: '1px solid var(--border, #eee)', verticalAlign: 'top' }}>
                    {cols.map((c) => {
                      const raw = r[c.key];
                      const danger =
                        (c.key === 'adEnabled' && raw === false) ||
                        (c.key === 'status') ||
                        (c.key === 'workdayStatus') ||
                        (c.key === 'hasD365Role' && raw === false);
                      return (
                        <td
                          key={c.key}
                          style={{ padding: '8px 12px', color: danger ? 'var(--danger, #c0392b)' : undefined }}
                        >
                          {cellText(raw)}
                        </td>
                      );
                    })}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}
    </div>
  );
}
