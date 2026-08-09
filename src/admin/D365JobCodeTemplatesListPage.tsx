import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useApi } from '../api/ApiContext';
import type { D365JobCodeTemplateSummaryDto } from '../api/types';

export function D365JobCodeTemplatesListPage() {
  const api = useApi();
  const [rows, setRows] = useState<D365JobCodeTemplateSummaryDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  useEffect(() => {
    setIsLoading(true);
    setLoadError(null);
    api.d365JobCodeTemplates
      .list()
      .then(setRows)
      .catch((err) => setLoadError(err instanceof Error ? err.message : 'Erreur inconnue'))
      .finally(() => setIsLoading(false));
  }, [api]);

  const filledCount = rows.filter((r) => r.isFilled).length;

  return (
    <div className="step-panel">
      <div className="step-panel__header">
        <div>
          <div className="step-panel__title">Formulaires D365 par code d'emploi</div>
          <div className="step-panel__subtitle">
            Un formulaire par code d'emploi capture toutes les réponses nécessaires au billet TDX « D365 - Access »
            (entité légale, numéro de département, limite d'approbation, rôles, etc.) — une fois rempli pour un code
            d'emploi, ces réponses pourront être utilisées pour créer automatiquement le billet TDX lors d'une
            nouvelle embauche.
          </div>
        </div>
      </div>

      {isLoading && <div>Chargement…</div>}
      {loadError && <div className="big-notice">{loadError}</div>}

      {!isLoading && !loadError && (
        <>
          <div style={{ marginBottom: 16, color: '#666' }}>
            {filledCount} / {rows.length} code(s) d'emploi rempli(s)
          </div>
          <table style={{ width: '100%', borderCollapse: 'collapse' }}>
            <thead>
              <tr style={{ textAlign: 'left', borderBottom: '2px solid var(--border, #ddd)' }}>
                <th style={{ padding: '8px 12px' }}>Code d'emploi</th>
                <th style={{ padding: '8px 12px' }}>Titre du poste</th>
                <th style={{ padding: '8px 12px' }}>Statut</th>
                <th style={{ padding: '8px 12px' }}></th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr key={row.jobCode} style={{ borderBottom: '1px solid var(--border, #eee)' }}>
                  <td style={{ padding: '8px 12px' }}>{row.jobCode}</td>
                  <td style={{ padding: '8px 12px', color: row.positionTitle ? undefined : '#888' }}>
                    {row.positionTitle ?? '—'}
                  </td>
                  <td style={{ padding: '8px 12px' }}>
                    {row.isFilled ? (
                      <span style={{ color: 'var(--success-text, #2e7d32)', fontWeight: 600 }}>Rempli</span>
                    ) : (
                      <span style={{ color: '#888' }}>Non rempli</span>
                    )}
                  </td>
                  <td style={{ padding: '8px 12px', textAlign: 'right' }}>
                    <Link to={`/admin/d365-jobcode-templates/${encodeURIComponent(row.jobCode)}`} className="review-section__edit">
                      {row.isFilled ? 'Modifier' : 'Remplir'}
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}
    </div>
  );
}
