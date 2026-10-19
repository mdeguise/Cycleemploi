import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useApi } from '../api/ApiContext';
import type { IssueFileDto, IssueFileInput, IssueImageDto, IssueReportDto, IssueStatus, IssueType } from '../api/types';
import { CheckCircleIcon, FlagIcon, PaperclipIcon, XIcon } from '../components/icons';
import { getScreen, snapshot } from './context';
import './issues.css';

export const TYPE_LABEL: Record<IssueType, string> = {
  BUG: 'Bogue',
  IMPROVEMENT: 'Amélioration',
  QUESTION: 'Question',
};

export const STATUS_LABEL: Record<IssueStatus, string> = {
  NEW: 'Nouveau',
  IN_PROGRESS: 'En cours',
  FIXED: 'Corrigé',
  REJECTED: 'Rejeté',
  DUPLICATE: 'Doublon',
};

export const STATUS_STYLE: Record<IssueStatus, { background: string; color: string }> = {
  NEW: { background: '#fdecee', color: '#7d1624' },
  IN_PROGRESS: { background: '#fff4d6', color: '#8a5a00' },
  FIXED: { background: '#dff5e3', color: '#1c6b34' },
  REJECTED: { background: '#e9e9ee', color: '#555' },
  DUPLICATE: { background: '#e9e9ee', color: '#555' },
};

const TYPE_HELP: Record<IssueType, string> = {
  BUG: "Quelque chose ne fonctionne pas",
  IMPROVEMENT: 'Une idée pour que ce soit mieux',
  QUESTION: "Je ne comprends pas comment faire",
};

const MAX_CAPTURES = 4;
const MAX_DOCS = 3;
const MAX_DOC_BYTES = 5 * 1024 * 1024;
const MAX_TOTAL_BYTES = 15 * 1024 * 1024;
const DOC_EXT = /\.(pdf|docx?|xlsx?)$/i;

/** Downscales a pasted screenshot to max 1920 px wide — PNG when it stays small, JPEG otherwise. */
async function prepareCapture(blob: Blob): Promise<string> {
  const bitmap = await createImageBitmap(blob);
  const scale = Math.min(1, 1920 / bitmap.width);
  const canvas = document.createElement('canvas');
  canvas.width = Math.round(bitmap.width * scale);
  canvas.height = Math.round(bitmap.height * scale);
  canvas.getContext('2d')!.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
  const png = canvas.toDataURL('image/png');
  return png.length * 0.75 <= 1.5 * 1024 * 1024 ? png : canvas.toDataURL('image/jpeg', 0.9);
}

function readDataUrl(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(reader.result as string);
    reader.onerror = () => reject(reader.error);
    reader.readAsDataURL(file);
  });
}

const approxBytes = (dataUrl: string) => Math.round((dataUrl.length - dataUrl.indexOf(',') - 1) * 0.75);

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

function StatusBadge({ status }: { status: IssueStatus }) {
  return (
    <span className="issue-badge" style={STATUS_STYLE[status]}>
      {STATUS_LABEL[status]}
    </span>
  );
}

function MyIssues({ onEdit }: { onEdit: (i: IssueReportDto) => void }) {
  const api = useApi();
  const { data, isLoading } = useQuery({ queryKey: ['issues', 'mine'], queryFn: () => api.issues.mine() });
  if (isLoading) return <div className="issue-muted">Chargement…</div>;
  if (!data?.length) return <div className="issue-muted">Vous n'avez encore fait aucun signalement.</div>;
  return (
    <table className="issue-table">
      <thead>
        <tr>
          <th>No</th>
          <th>Date</th>
          <th>Titre</th>
          <th>Statut</th>
          <th>Suivi</th>
          <th />
        </tr>
      </thead>
      <tbody>
        {data.map((i) => (
          <tr key={i.issueId}>
            <td>#{i.issueId}</td>
            <td>{new Date(i.createdUtc).toLocaleDateString('fr-CA')}</td>
            <td>{i.title}</td>
            <td>
              <StatusBadge status={i.status} />
            </td>
            <td>{i.resolution ?? '—'}</td>
            <td>
              {i.status === 'NEW' && (
                <button type="button" className="issue-link" onClick={() => onEdit(i)}>
                  Modifier
                </button>
              )}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

interface Props {
  /** When set, the modal edits this (still NEW) report instead of creating one. */
  edit?: IssueReportDto | null;
  onClose: () => void;
}

export function ReportIssueModal({ edit: initialEdit, onClose }: Props) {
  const api = useApi();
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState<IssueReportDto | null>(initialEdit ?? null);
  const [type, setType] = useState<IssueType>(initialEdit?.type ?? 'BUG');
  const [title, setTitle] = useState(initialEdit?.title ?? '');
  const [description, setDescription] = useState(initialEdit?.description ?? '');
  const [expected, setExpected] = useState(initialEdit?.expected ?? '');
  const [captures, setCaptures] = useState<string[]>([]);
  const [docs, setDocs] = useState<IssueFileInput[]>([]);
  const [keepImages, setKeepImages] = useState<IssueImageDto[]>(initialEdit?.images ?? []);
  const [keepFiles, setKeepFiles] = useState<IssueFileDto[]>(initialEdit?.files ?? []);
  const [showMine, setShowMine] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sentNumber, setSentNumber] = useState<number | null>(null);
  const [dragOver, setDragOver] = useState(false);

  // Frozen at open — the report describes what the user saw, not what they did afterwards.
  const context = useMemo(() => snapshot(), []);
  const screen = useMemo(() => getScreen(), []);

  const addImages = async (blobs: Blob[]) => {
    for (const blob of blobs) {
      if (keepImages.length + captures.length >= MAX_CAPTURES) {
        setError(`Maximum ${MAX_CAPTURES} captures.`);
        return;
      }
      try {
        const dataUrl = await prepareCapture(blob);
        setCaptures((prev) => (keepImages.length + prev.length >= MAX_CAPTURES ? prev : [...prev, dataUrl]));
      } catch {
        setError("Impossible de lire cette image.");
      }
    }
  };

  const addDocs = async (files: File[]) => {
    for (const file of files) {
      if (!DOC_EXT.test(file.name)) {
        setError(`« ${file.name} » : seuls les PDF, Word et Excel sont acceptés.`);
        continue;
      }
      if (file.size > MAX_DOC_BYTES) {
        setError(`« ${file.name} » dépasse 5 Mo.`);
        continue;
      }
      if (keepFiles.length + docs.length >= MAX_DOCS) {
        setError(`Maximum ${MAX_DOCS} documents.`);
        break;
      }
      const dataUrl = await readDataUrl(file);
      setDocs((prev) => (keepFiles.length + prev.length >= MAX_DOCS ? prev : [...prev, { fileName: file.name, dataUrl }]));
    }
  };

  // Ctrl+V anywhere in the modal: screenshots become captures, copied PDF/Word/Excel files documents.
  useEffect(() => {
    const onPaste = (ev: ClipboardEvent) => {
      const files = Array.from(ev.clipboardData?.files ?? []);
      if (!files.length) return;
      const images = files.filter((f) => f.type.startsWith('image/'));
      const documents = files.filter((f) => DOC_EXT.test(f.name));
      if (!images.length && !documents.length) return;
      ev.preventDefault();
      setError(null);
      void addImages(images);
      void addDocs(documents);
    };
    window.addEventListener('paste', onPaste);
    return () => window.removeEventListener('paste', onPaste);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [keepImages.length, keepFiles.length, captures.length, docs.length]);

  const totalBytes =
    captures.reduce((n, c) => n + approxBytes(c), 0) +
    docs.reduce((n, d) => n + approxBytes(d.dataUrl), 0);

  const send = useMutation({
    mutationFn: async () => {
      // Re-open the NTLM connection first — a POST on a connection the server already closed fails
      // with an opaque "Failed to fetch" (see client.ts).
      await api.auth.me().catch(() => undefined);
      if (editing) {
        return api.issues.edit(editing.issueId, {
          type,
          title,
          description,
          expected: expected || null,
          removeAttachmentIds: [
            ...editing.images.filter((i) => !keepImages.some((k) => k.imageId === i.imageId)).map((i) => i.imageId),
            ...editing.files.filter((f) => !keepFiles.some((k) => k.fileId === f.fileId)).map((f) => f.fileId),
          ],
          captures,
          files: docs,
        });
      }
      return api.issues.create({
        type,
        title,
        description,
        expected: expected || null,
        screen,
        context: JSON.stringify(context),
        captures,
        files: docs,
      });
    },
    onSuccess: (issue) => {
      setSentNumber(issue.issueId);
      void queryClient.invalidateQueries({ queryKey: ['issues'] });
    },
    onError: (err) => setError(messageOf(err)),
  });

  const canSend = title.trim() && description.trim() && totalBytes <= MAX_TOTAL_BYTES && !send.isPending;

  const startEdit = (i: IssueReportDto) => {
    setEditing(i);
    setType(i.type);
    setTitle(i.title);
    setDescription(i.description);
    setExpected(i.expected ?? '');
    setKeepImages(i.images);
    setKeepFiles(i.files);
    setCaptures([]);
    setDocs([]);
    setShowMine(false);
    setError(null);
  };

  if (sentNumber !== null) {
    return (
      <div className="modal-overlay" onClick={onClose}>
        <div className="modal-card" onClick={(ev) => ev.stopPropagation()}>
          <span className="modal-card__icon">
            <CheckCircleIcon style={{ width: 28, height: 28 }} />
          </span>
          <div className="modal-card__title">{editing ? 'Signalement modifié' : 'Signalement envoyé'}</div>
          <p className="modal-card__text">
            Merci! Votre signalement porte le numéro <strong>#{sentNumber}</strong>. Vous pouvez en suivre l'état dans
            « Mes signalements ».
          </p>
          <div style={{ display: 'flex', gap: 8, justifyContent: 'center' }}>
            <button
              type="button"
              className="btn btn-secondary"
              onClick={() => {
                setSentNumber(null);
                setEditing(null);
                setShowMine(true);
              }}
            >
              Voir mes signalements
            </button>
            <button type="button" className="btn btn-primary" onClick={onClose}>
              Fermer
            </button>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="issue-modal" onClick={(ev) => ev.stopPropagation()}>
        <div className="issue-modal__head">
          <span className="issue-modal__title">
            <FlagIcon style={{ width: 18, height: 18 }} />
            {editing ? `Modifier le signalement #${editing.issueId}` : 'Signaler un problème'}
          </span>
          <button type="button" className="issue-link" onClick={onClose} aria-label="Fermer">
            <XIcon style={{ width: 18, height: 18 }} />
          </button>
        </div>

        <div className="issue-modal__body">
          {showMine ? (
            <MyIssues onEdit={startEdit} />
          ) : (
            <>
              <div className="issue-types">
                {(Object.keys(TYPE_LABEL) as IssueType[]).map((t) => (
                  <label key={t} className={type === t ? 'active' : ''}>
                    <input type="radio" name="issue-type" checked={type === t} onChange={() => setType(t)} />
                    <strong>{TYPE_LABEL[t]}</strong>
                    <span>{TYPE_HELP[t]}</span>
                  </label>
                ))}
              </div>

              <label className="issue-field">
                <span>Titre *</span>
                <input type="text" maxLength={200} value={title} onChange={(ev) => setTitle(ev.target.value)} />
              </label>
              <label className="issue-field">
                <span>Que s'est-il passé? *</span>
                <textarea rows={4} value={description} onChange={(ev) => setDescription(ev.target.value)} />
              </label>
              <label className="issue-field">
                <span>À quoi vous attendiez-vous? (facultatif)</span>
                <textarea rows={2} value={expected} onChange={(ev) => setExpected(ev.target.value)} />
              </label>

              <div className="issue-field">
                <span>
                  Captures d'écran ({keepImages.length + captures.length}/{MAX_CAPTURES}) — copiez une image puis
                  faites <kbd>Ctrl</kbd>+<kbd>V</kbd> ici
                </span>
                <div className="issue-captures">
                  {keepImages.map((img) => (
                    <figure key={img.imageId}>
                      <img src={api.issues.imageUrl(editing!.issueId, img.imageId)} alt="" />
                      <button type="button" className="issue-remove" onClick={() => setKeepImages((p) => p.filter((x) => x !== img))}>
                        <XIcon style={{ width: 12, height: 12 }} />
                      </button>
                    </figure>
                  ))}
                  {captures.map((c, idx) => (
                    <figure key={idx}>
                      <img src={c} alt="" />
                      <button type="button" className="issue-remove" onClick={() => setCaptures((p) => p.filter((_, n) => n !== idx))}>
                        <XIcon style={{ width: 12, height: 12 }} />
                      </button>
                    </figure>
                  ))}
                </div>
              </div>

              <div className="issue-field">
                <span>
                  Documents ({keepFiles.length + docs.length}/{MAX_DOCS}) — PDF, Word ou Excel, 5 Mo maximum
                </span>
                <label
                  className={`issue-drop${dragOver ? ' over' : ''}`}
                  onDragOver={(ev) => {
                    ev.preventDefault();
                    setDragOver(true);
                  }}
                  onDragLeave={() => setDragOver(false)}
                  onDrop={(ev) => {
                    ev.preventDefault();
                    setDragOver(false);
                    setError(null);
                    void addDocs(Array.from(ev.dataTransfer.files));
                  }}
                >
                  <PaperclipIcon style={{ width: 16, height: 16 }} /> Déposez un fichier ici ou cliquez pour choisir
                  <input
                    type="file"
                    hidden
                    multiple
                    accept=".pdf,.doc,.docx,.xls,.xlsx"
                    onChange={(ev) => {
                      setError(null);
                      void addDocs(Array.from(ev.target.files ?? []));
                      ev.target.value = '';
                    }}
                  />
                </label>
                <div className="issue-docs">
                  {keepFiles.map((f) => (
                    <span key={f.fileId} className="issue-chip">
                      {f.fileName}
                      <button type="button" onClick={() => setKeepFiles((p) => p.filter((x) => x !== f))}>
                        <XIcon style={{ width: 12, height: 12 }} />
                      </button>
                    </span>
                  ))}
                  {docs.map((d, idx) => (
                    <span key={idx} className="issue-chip">
                      {d.fileName}
                      <button type="button" onClick={() => setDocs((p) => p.filter((_, n) => n !== idx))}>
                        <XIcon style={{ width: 12, height: 12 }} />
                      </button>
                    </span>
                  ))}
                </div>
              </div>

              {!editing && (
                <details className="issue-context">
                  <summary>
                    Joint automatiquement : {screen || 'écran courant'} · {context.recentErrors.length} erreur(s) récente(s)
                  </summary>
                  <pre>{JSON.stringify(context, null, 2)}</pre>
                </details>
              )}
            </>
          )}

          {error && <div className="issue-error">{error}</div>}
        </div>

        <div className="issue-modal__foot">
          <button type="button" className="issue-link" onClick={() => setShowMine((v) => !v)}>
            {showMine ? '← Retour au formulaire' : 'Mes signalements'}
          </button>
          {!showMine && (
            <div style={{ display: 'flex', gap: 8 }}>
              <button type="button" className="btn btn-secondary" onClick={onClose}>
                Annuler
              </button>
              <button type="button" className="btn btn-primary" disabled={!canSend} onClick={() => {
                setError(null);
                send.mutate();
              }}>
                {send.isPending ? 'Envoi…' : editing ? 'Enregistrer' : 'Envoyer'}
              </button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

export { StatusBadge };
