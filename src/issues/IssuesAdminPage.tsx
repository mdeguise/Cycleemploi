import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useApi } from '../api/ApiContext';
import type { IssueReportDto, IssueStatus } from '../api/types';
import { CopyIcon, DownloadIcon, FlagIcon } from '../components/icons';
import { STATUS_LABEL, TYPE_LABEL, StatusBadge } from './ReportIssueModal';
import './issues.css';

const FILTERS: { value: string; label: string }[] = [
  { value: 'open', label: 'Ouverts' },
  { value: '', label: 'Tous' },
  { value: 'FIXED', label: 'Corrigés' },
  { value: 'REJECTED', label: 'Rejetés' },
  { value: 'DUPLICATE', label: 'Doublons' },
];

/** The prod site is plain HTTP, where the async Clipboard API is unavailable — fall back to a
 * hidden textarea + execCommand, and finally to showing the text for a manual copy. */
async function copyText(text: string): Promise<boolean> {
  try {
    if (window.isSecureContext && navigator.clipboard) {
      await navigator.clipboard.writeText(text);
      return true;
    }
  } catch {
    /* fall through */
  }
  const ta = document.createElement('textarea');
  ta.value = text;
  ta.style.position = 'fixed';
  ta.style.opacity = '0';
  document.body.appendChild(ta);
  ta.select();
  let ok = false;
  try {
    ok = document.execCommand('copy');
  } catch {
    ok = false;
  }
  document.body.removeChild(ta);
  return ok;
}

function IssueDetail({ id, onEdit }: { id: number; onEdit: (i: IssueReportDto) => void }) {
  const api = useApi();
  const queryClient = useQueryClient();
  const { data: issue } = useQuery({ queryKey: ['issues', 'detail', id], queryFn: () => api.issues.get(id) });

  const [status, setStatus] = useState<IssueStatus>('NEW');
  const [commit, setCommit] = useState('');
  const [resolution, setResolution] = useState('');
  const [work, setWork] = useState('');
  const [message, setMessage] = useState<string | null>(null);
  const [manualCopy, setManualCopy] = useState<string | null>(null);

  useEffect(() => {
    if (!issue) return;
    setStatus(issue.status);
    setCommit(issue.commitRef ?? '');
    setResolution(issue.resolution ?? '');
    setWork('');
    setMessage(null);
    setManualCopy(null);
  }, [issue]);

  const save = useMutation({
    mutationFn: () =>
      api.issues.update(id, { status, resolution: resolution || null, commitRef: commit || null, workLog: work || null }),
    onSuccess: () => {
      setMessage('Suivi enregistré.');
      void queryClient.invalidateQueries({ queryKey: ['issues'] });
    },
    onError: (err) => {
      const raw = err instanceof Error ? err.message : String(err);
      let msg = raw;
      try {
        msg = JSON.parse(raw).error ?? raw;
      } catch {
        /* plain text */
      }
      setMessage(msg);
    },
  });

  const copyForClaude = async () => {
    const md = await api.issues.markdown(id);
    if (!(await copyText(md))) setManualCopy(md);
    else setMessage('Copié — collez-le dans Claude.');
  };

  if (!issue) return <div className="issue-muted">Chargement…</div>;

  let context: string | null = null;
  if (issue.context) {
    try {
      context = JSON.stringify(JSON.parse(issue.context), null, 2);
    } catch {
      context = issue.context;
    }
  }

  return (
    <div className="issue-detail">
      <div className="issue-detail__head">
        <h3>
          #{issue.issueId} — {issue.title}
        </h3>
        <div style={{ display: 'flex', gap: 8 }}>
          {issue.status === 'NEW' && (
            <button type="button" className="btn btn-secondary" onClick={() => onEdit(issue)}>
              Modifier
            </button>
          )}
          <button type="button" className="btn btn-secondary" onClick={copyForClaude}>
            <CopyIcon style={{ width: 14, height: 14, marginRight: 6 }} />
            Copier pour Claude
          </button>
        </div>
      </div>

      <dl className="issue-pairs">
        <dt>Type</dt>
        <dd>{TYPE_LABEL[issue.type]}</dd>
        <dt>Signalé par</dt>
        <dd>
          {issue.createdByName ?? issue.createdBySam} — {new Date(issue.createdUtc).toLocaleString('fr-CA')}
        </dd>
        <dt>Écran</dt>
        <dd>{issue.screen || '—'}</dd>
        <dt>Statut</dt>
        <dd>
          <StatusBadge status={issue.status} />
        </dd>
      </dl>

      <h4>Description</h4>
      <p className="issue-prewrap">{issue.description}</p>
      {issue.expected && (
        <>
          <h4>Comportement attendu</h4>
          <p className="issue-prewrap">{issue.expected}</p>
        </>
      )}

      {issue.images.length > 0 && (
        <>
          <h4>Captures</h4>
          <div className="issue-captures">
            {issue.images.map((img) => (
              <figure key={img.imageId}>
                <a href={api.issues.imageUrl(issue.issueId, img.imageId)} target="_blank" rel="noreferrer">
                  <img src={api.issues.imageUrl(issue.issueId, img.imageId)} alt="" />
                </a>
              </figure>
            ))}
          </div>
        </>
      )}
      {issue.files.length > 0 && (
        <>
          <h4>Documents joints</h4>
          <div className="issue-docs">
            {issue.files.map((f) => (
              <a key={f.fileId} className="issue-chip" href={api.issues.fileUrl(issue.issueId, f.fileId)}>
                <DownloadIcon style={{ width: 12, height: 12 }} /> {f.fileName}
              </a>
            ))}
          </div>
        </>
      )}
      {context && (
        <details className="issue-context">
          <summary>Contexte technique</summary>
          <pre>{context}</pre>
        </details>
      )}

      <h4>Suivi</h4>
      <div className="issue-followup">
        <label className="issue-field">
          <span>Statut</span>
          <select value={status} onChange={(ev) => setStatus(ev.target.value as IssueStatus)}>
            {(Object.keys(STATUS_LABEL) as IssueStatus[]).map((s) => (
              <option key={s} value={s}>
                {STATUS_LABEL[s]}
              </option>
            ))}
          </select>
        </label>
        <label className="issue-field">
          <span>Commit</span>
          <input type="text" maxLength={40} value={commit} onChange={(ev) => setCommit(ev.target.value)} />
        </label>
        <label className="issue-field" style={{ gridColumn: '1 / -1' }}>
          <span>Résolution (visible par la personne qui a signalé)</span>
          <textarea rows={3} value={resolution} onChange={(ev) => setResolution(ev.target.value)} />
        </label>
        <label className="issue-field" style={{ gridColumn: '1 / -1' }}>
          <span>Travaux (fichiers modifiés, logique, raison — s'ajoute au journal)</span>
          <textarea rows={3} value={work} onChange={(ev) => setWork(ev.target.value)} />
        </label>
      </div>
      {issue.workLog && (
        <details className="issue-context" open>
          <summary>Journal des travaux</summary>
          <pre>{issue.workLog}</pre>
        </details>
      )}
      <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginTop: 12 }}>
        <button type="button" className="btn btn-primary" disabled={save.isPending} onClick={() => save.mutate()}>
          Enregistrer le suivi
        </button>
        {message && <span className="issue-muted">{message}</span>}
      </div>
      {manualCopy && (
        <label className="issue-field" style={{ marginTop: 12 }}>
          <span>Copie automatique impossible — sélectionnez et copiez ce texte :</span>
          <textarea readOnly rows={8} value={manualCopy} onFocus={(ev) => ev.target.select()} />
        </label>
      )}
    </div>
  );
}

export function IssuesAdminPage({ onEdit }: { onEdit: (i: IssueReportDto) => void }) {
  const api = useApi();
  const initialId = Number(new URLSearchParams(window.location.search).get('issue')) || null;
  const [filter, setFilter] = useState(initialId ? '' : 'open');
  const [selectedId, setSelectedId] = useState<number | null>(initialId);
  const { data: list = [], isLoading } = useQuery({
    queryKey: ['issues', 'list', filter],
    queryFn: () => api.issues.list(filter || undefined),
  });

  return (
    <div className="step-panel" style={{ maxWidth: 1200, margin: '0 auto', width: '100%' }}>
      <div className="step-panel__header">
        <span className="step-panel__icon">
          <FlagIcon style={{ width: 22, height: 22 }} />
        </span>
        <div>
          <div className="step-panel__title">Signalements</div>
          <div className="step-panel__subtitle">Problèmes, idées et questions signalés par les utilisateurs de Cycle Emploi.</div>
        </div>
      </div>

      <div className="issues-layout">
        <div>
          <div style={{ display: 'flex', gap: 8, marginBottom: 10 }}>
            <select value={filter} onChange={(ev) => setFilter(ev.target.value)} style={{ flex: 1 }}>
              {FILTERS.map((f) => (
                <option key={f.value} value={f.value}>
                  {f.label}
                </option>
              ))}
            </select>
            <a className="btn btn-secondary" href={api.issues.exportUrl(filter || undefined)} style={{ textDecoration: 'none' }}>
              Excel
            </a>
          </div>
          {isLoading && <div className="issue-muted">Chargement…</div>}
          {!isLoading && list.length === 0 && <div className="issue-muted">Aucun signalement.</div>}
          <ul className="issue-list">
            {list.map((i) => (
              <li key={i.issueId} className={i.issueId === selectedId ? 'selected' : ''} onClick={() => setSelectedId(i.issueId)}>
                <span className="issue-no">#{i.issueId}</span>
                <span className="issue-main">
                  <span className="issue-title">{i.title}</span>
                  <span className="issue-muted">
                    {TYPE_LABEL[i.type]} · {i.createdByName ?? i.createdBySam} · {new Date(i.createdUtc).toLocaleDateString('fr-CA')}
                    {i.images.length > 0 && ` · ${i.images.length} capture(s)`}
                    {i.files.length > 0 && ` · ${i.files.length} doc(s)`}
                  </span>
                </span>
                <StatusBadge status={i.status} />
              </li>
            ))}
          </ul>
        </div>
        <div>{selectedId ? <IssueDetail key={selectedId} id={selectedId} onEdit={onEdit} /> : <div className="issue-muted">Choisissez un signalement.</div>}</div>
      </div>
    </div>
  );
}
