import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useApi } from '../api/ApiContext';

/** The API answers validation errors as { error: "…" } — show just the message. */
function messageOf(err: unknown): string {
  const raw = err instanceof Error ? err.message : String(err);
  try {
    const parsed = JSON.parse(raw);
    if (parsed?.error) return String(parsed.error);
  } catch {
    /* not JSON */
  }
  return raw;
}

/**
 * Fil « Échange » d'un signalement : ce qui s'est dit et décidé, gardé avec le signalement (consigne de Marc,
 * 2026-11-10 — les réponses vivent DANS le signalement, jamais par courriel). Les réponses de la personne qui a signalé
 * et de l'équipe s'y trouvent avec l'auteur et la date. Écrire ici avise l'autre côté par courriel (case décochable).
 */
export function IssueThread({ issueId, requesterName, compact }: { issueId: number; requesterName?: string | null; compact?: boolean }) {
  const api = useApi();
  const queryClient = useQueryClient();
  const { data, isLoading } = useQuery({ queryKey: ['issue-thread', issueId], queryFn: () => api.issues.messages(issueId) });
  const [text, setText] = useState('');
  const [notify, setNotify] = useState(true);
  const send = useMutation({
    mutationFn: () => api.issues.postMessage(issueId, text, notify),
    onSuccess: () => {
      setText('');
      void queryClient.invalidateQueries({ queryKey: ['issue-thread', issueId] });
      void queryClient.invalidateQueries({ queryKey: ['issues'] });
    },
  });
  const rows = data ?? [];
  const fieldId = `issue-thread-${issueId}`;
  return (
    <div className="issue-thread">
      {isLoading && <div className="issue-muted">Chargement…</div>}
      {!isLoading && rows.length === 0 && (
        <div className="issue-muted">Aucun message pour l’instant. Écrivez ici : la réponse est gardée avec le signalement.</div>
      )}
      {rows.map((m) => (
        <div key={m.messageId} className={`issue-msg ${m.fromTeam ? 'team' : 'req'}`}>
          <div className="issue-msg__head">
            <strong>{m.authorName ?? (m.fromTeam ? 'Équipe' : requesterName ?? 'Demandeur')}</strong>
            <span className="issue-muted">
              {' '}· {m.fromTeam ? 'équipe' : 'demandeur'} · {new Date(m.createdUtc).toLocaleString('fr-CA')}
            </span>
          </div>
          <div className="issue-prewrap">{m.body}</div>
        </div>
      ))}
      <label className="issue-field" htmlFor={fieldId}>
        <span>Votre réponse ou votre précision</span>
      </label>
      <textarea id={fieldId} rows={compact ? 2 : 3} maxLength={8000} value={text} onChange={(ev) => setText(ev.target.value)} />
      <div className="issue-thread__actions">
        <button type="button" className="btn btn-primary" disabled={send.isPending || text.trim().length === 0} onClick={() => send.mutate()}>
          {send.isPending ? 'Envoi…' : 'Envoyer'}
        </button>
        <label className="issue-check">
          <input type="checkbox" checked={notify} onChange={(ev) => setNotify(ev.target.checked)} />
          Aviser l’autre personne par courriel
        </label>
        {send.isError && <span className="issue-error-text">{messageOf(send.error)}</span>}
      </div>
    </div>
  );
}
