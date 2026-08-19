import { useEffect, useState } from 'react';
import { useApi } from '../api/ApiContext';
import type { TicketTemplateDto } from '../api/types';

function TemplateCard({ template, onSaved }: { template: TicketTemplateDto; onSaved: (updated: TicketTemplateDto) => void }) {
  const api = useApi();
  const [content, setContent] = useState(template.content);
  const [isSaving, setIsSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);

  const isDirty = content !== template.content;
  const isDefault = content === template.defaultContent;

  const handleSave = async () => {
    setSaveError(null);
    setSaved(false);
    setIsSaving(true);
    try {
      const updated = await api.ticketTemplates.update(template.key, { content });
      onSaved(updated);
      setSaved(true);
    } catch (err) {
      setSaveError(err instanceof Error ? err.message : 'Erreur inconnue');
    } finally {
      setIsSaving(false);
    }
  };

  const handleReset = () => {
    setContent(template.defaultContent);
    setSaved(false);
  };

  return (
    <div className="step-panel" style={{ marginBottom: 20 }}>
      <div className="step-panel__header">
        <div>
          <div className="step-panel__title">{template.label}</div>
          <div className="step-panel__subtitle">{template.description}</div>
        </div>
      </div>

      <div style={{ display: 'flex', gap: 20, alignItems: 'flex-start' }}>
        <div style={{ flex: 2 }}>
          <div className="field">
            <label className="field__label">Contenu</label>
            <textarea
              value={content}
              onChange={(ev) => {
                setContent(ev.target.value);
                setSaved(false);
              }}
              rows={14}
              style={{ fontFamily: 'monospace', fontSize: 12.5 }}
            />
          </div>

          <div style={{ fontSize: 12, color: 'var(--muted)', marginTop: 6 }}>
            {template.updatedAt ? (
              <>
                Modifié le {new Date(template.updatedAt).toLocaleString('fr-CA')}
                {template.updatedByDisplayName ? ` par ${template.updatedByDisplayName}` : ''}
                {isDefault ? ' — valeur par défaut' : ''}
              </>
            ) : (
              'Jamais modifié — valeur par défaut'
            )}
          </div>

          {saveError && (
            <div className="required-note" style={{ color: 'var(--tremblant-red-dark)', marginTop: 10 }}>
              {saveError}
            </div>
          )}

          <div style={{ display: 'flex', gap: 10, marginTop: 12 }}>
            <button type="button" className="btn btn-primary" onClick={handleSave} disabled={isSaving || !isDirty}>
              {isSaving ? 'Enregistrement…' : 'Enregistrer'}
            </button>
            <button type="button" className="btn btn-secondary" onClick={handleReset} disabled={isDefault}>
              Réinitialiser à la valeur par défaut
            </button>
            {saved && !isDirty && <span style={{ color: 'var(--success-text, #2e7d32)', alignSelf: 'center' }}>Enregistré ✓</span>}
          </div>
        </div>

        <div style={{ flex: 1, minWidth: 260 }}>
          <div style={{ fontSize: 13, fontWeight: 600, marginBottom: 8 }}>Variables disponibles</div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
            {template.placeholders.map((p) => (
              <div key={p.name} style={{ fontSize: 12, lineHeight: 1.4 }}>
                <code style={{ background: 'var(--bg-page)', padding: '1px 5px', borderRadius: 4 }}>{`{{${p.name}}}`}</code>
                <div style={{ color: 'var(--muted)' }}>{p.description}</div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}

export function TicketTemplatesAdminPage() {
  const api = useApi();
  const [templates, setTemplates] = useState<TicketTemplateDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  useEffect(() => {
    setIsLoading(true);
    setLoadError(null);
    api.ticketTemplates
      .list()
      .then(setTemplates)
      .catch((err) => setLoadError(err instanceof Error ? err.message : 'Erreur inconnue'))
      .finally(() => setIsLoading(false));
  }, [api]);

  const handleSaved = (updated: TicketTemplateDto) => {
    setTemplates((current) => current.map((t) => (t.key === updated.key ? updated : t)));
  };

  return (
    <div>
      <div className="step-panel__header" style={{ marginBottom: 16 }}>
        <div>
          <div className="step-panel__title">Gabarits des billets</div>
          <div className="step-panel__subtitle">
            Contrôle le contenu envoyé à chaque système (Freshdesk, TDX) lors de la soumission d'une demande. Les
            variables entre accolades doubles (ex. {'{{EmployeeName}}'}) sont remplacées par les vraies valeurs de
            la demande au moment de la création du billet — un champ vide s'affiche comme « — ».
          </div>
        </div>
      </div>

      {isLoading && <div>Chargement…</div>}
      {loadError && <div className="big-notice">{loadError}</div>}

      {!isLoading && !loadError && templates.map((t) => <TemplateCard key={t.key} template={t} onSaved={handleSaved} />)}
    </div>
  );
}
