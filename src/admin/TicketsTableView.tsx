import type { LiveTicketState, TicketRefDto, TicketViewRowDto } from '../api/types';

const TYPE_LABELS: Record<string, string> = {
  Onboarding: 'Nouvelle intégration',
  Reactivation: 'Réactivation',
  Offboarding: 'Cessation',
};

const STATE_STYLE: Record<LiveTicketState, { bg: string; text: string }> = {
  Open: { bg: 'var(--ok-bg, #1c7c3c)', text: 'OUVERT' },
  Closed: { bg: 'var(--muted-bg, #6b7280)', text: 'FERMÉ' },
  // Distinct from FERMÉ on purpose — the source system could not be reached, and saying "closed"
  // would be a wrong answer someone acts on.
  Unknown: { bg: 'var(--warn-bg, #b8860b)', text: 'INCONNU' },
};

function TicketCell({ tickets }: { tickets: TicketRefDto[] }) {
  if (tickets.length === 0) return <span style={{ color: 'var(--muted, #888)' }}>—</span>;
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
      {tickets.map((t) => {
        const style = STATE_STYLE[t.state] ?? STATE_STYLE.Unknown;
        return (
          <div key={t.requestTicketId} style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            <span style={{ fontFamily: 'monospace', fontWeight: 600 }}>{t.ticketNumber}</span>
            <span
              title={t.stateLabel ?? undefined}
              style={{ background: style.bg, color: '#fff', borderRadius: 9, padding: '1px 7px', fontSize: 11, fontWeight: 700 }}
            >
              {style.text}
            </span>
            {t.employeeName && (
              <span style={{ color: 'var(--muted, #666)', fontSize: 12 }}>{t.employeeName}</span>
            )}
          </div>
        );
      })}
    </div>
  );
}

/** The flat operational view: one row per request, every Freshdesk and TDX number with its live
 *  state. Deliberately does NOT repeat the request's entered data — that is what the detail view is
 *  for; this one is for scanning ticket numbers and spotting what is still open. */
export function TicketsTableView({
  rows,
  hasUnknownStatuses,
  onSelect,
}: {
  rows: TicketViewRowDto[];
  hasUnknownStatuses: boolean;
  onSelect: (requestId: number) => void;
}) {
  return (
    <>
      {hasUnknownStatuses && (
        <div className="big-notice" style={{ marginBottom: 8 }}>
          Certains statuts n'ont pas pu être récupérés (Freshdesk ou TDX injoignable). Ils sont marqués
          « INCONNU » plutôt que supposés fermés.
        </div>
      )}
      <div style={{ maxHeight: '64vh', overflow: 'auto', border: '1px solid var(--border, #ddd)' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse' }}>
          <thead>
            <tr
              style={{
                textAlign: 'left',
                borderBottom: '2px solid var(--border, #ddd)',
                position: 'sticky',
                top: 0,
                background: 'var(--bg-card, #fff)',
              }}
            >
              <th style={{ padding: '8px 10px', whiteSpace: 'nowrap' }}>N° demande</th>
              <th style={{ padding: '8px 10px' }}>Employé(s)</th>
              <th style={{ padding: '8px 10px' }}>Billets Freshdesk</th>
              <th style={{ padding: '8px 10px' }}>Billets TDX</th>
              <th style={{ padding: '8px 10px' }}></th>
            </tr>
          </thead>
          <tbody>
            {rows.length === 0 && (
              <tr><td colSpan={5} style={{ padding: 12 }}>Aucune demande ne correspond aux filtres.</td></tr>
            )}
            {rows.map((row) => (
              <tr
                key={row.requestId}
                onClick={() => onSelect(row.requestId)}
                style={{ borderBottom: '1px solid var(--border, #eee)', cursor: 'pointer', verticalAlign: 'top' }}
              >
                <td style={{ padding: '8px 10px', whiteSpace: 'nowrap' }}>
                  <strong>{row.requestNumber}</strong>
                  <div style={{ fontSize: 12, color: 'var(--muted, #666)' }}>
                    {TYPE_LABELS[row.requestType] ?? row.requestType}
                  </div>
                </td>
                <td style={{ padding: '8px 10px' }}>{row.employeeNames.join(', ') || '—'}</td>
                <td style={{ padding: '8px 10px' }}><TicketCell tickets={row.freshdesk} /></td>
                <td style={{ padding: '8px 10px' }}><TicketCell tickets={row.tdx} /></td>
                <td style={{ padding: '8px 10px', textAlign: 'right', whiteSpace: 'nowrap' }}>
                  {row.failedCount > 0 && (
                    <span style={{ background: 'var(--tremblant-red-dark, #b00)', color: '#fff', borderRadius: 10, padding: '2px 8px', fontSize: 12 }}>
                      {row.failedCount} à corriger
                    </span>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}
