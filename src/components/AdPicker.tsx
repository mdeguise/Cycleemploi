import { useEffect, useRef, useState } from 'react';
import type { AdAccountDto } from '../api/types';

/** Shared AD people-picker: the account is PICKED from AD rather than typed, because the
 * sAMAccountName is the authorization key and a typo would silently create a row that can never
 * match anyone. Reused everywhere a person needs to be attached to a D365Approver/D365Viewer row —
 * Approbateurs D365, Personnel TI, and per-position-title assignment. */
export function usePicker(search: (q: string) => Promise<AdAccountDto[]>) {
  const [query, setQuery] = useState('');
  const [hits, setHits] = useState<AdAccountDto[]>([]);
  const [isSearching, setIsSearching] = useState(false);
  const [picked, setPicked] = useState<AdAccountDto | null>(null);
  const searchSeq = useRef(0);

  useEffect(() => {
    if (picked || query.trim().length < 2) {
      setHits([]);
      return;
    }
    const seq = ++searchSeq.current;
    const timer = setTimeout(() => {
      setIsSearching(true);
      search(query.trim())
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
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [query, picked]);

  const reset = () => {
    setPicked(null);
    setQuery('');
    setHits([]);
  };

  return { query, setQuery, hits, isSearching, picked, setPicked, reset };
}

export function PickerField({ picker }: { picker: ReturnType<typeof usePicker> }) {
  const { query, setQuery, hits, isSearching, picked, setPicked } = picker;
  return (
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
                onClick={() => { setPicked(h); }}
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
  );
}
