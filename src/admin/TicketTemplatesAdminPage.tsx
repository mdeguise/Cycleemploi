import { useEffect, useState } from 'react';
import { useApi } from '../api/ApiContext';
import type {
  BlockTemplateContent,
  EmployeeFieldLine,
  InlinePart,
  InlineTemplateContent,
  TemplateBlock,
  TicketTemplateDto,
  TicketTemplateFieldDto,
} from '../api/types';

const rowStyle: React.CSSProperties = {
  display: 'flex',
  gap: 8,
  alignItems: 'center',
  padding: 8,
  background: 'var(--bg-page)',
  borderRadius: 6,
  marginBottom: 6,
};
const smallBtnStyle: React.CSSProperties = {
  padding: '4px 8px',
  border: '1px solid var(--border)',
  borderRadius: 4,
  background: 'var(--bg-card)',
  cursor: 'pointer',
  fontSize: 13,
  lineHeight: 1,
};
const addBtnStyle: React.CSSProperties = {
  padding: '6px 12px',
  border: '1px dashed var(--muted)',
  borderRadius: 6,
  background: 'none',
  fontSize: 13,
  cursor: 'pointer',
};

function MoveRemove({ onUp, onDown, onRemove }: { onUp?: () => void; onDown?: () => void; onRemove: () => void }) {
  return (
    <div style={{ display: 'flex', gap: 4, marginLeft: 'auto' }}>
      <button type="button" style={smallBtnStyle} onClick={onUp} disabled={!onUp} title="Déplacer vers le haut">
        ↑
      </button>
      <button type="button" style={smallBtnStyle} onClick={onDown} disabled={!onDown} title="Déplacer vers le bas">
        ↓
      </button>
      <button type="button" style={{ ...smallBtnStyle, color: 'var(--tremblant-red-dark)' }} onClick={onRemove} title="Retirer">
        ✕
      </button>
    </div>
  );
}

function move<T>(list: T[], from: number, to: number): T[] {
  const copy = [...list];
  const [item] = copy.splice(from, 1);
  copy.splice(to, 0, item);
  return copy;
}

// ---------- Inline editor (Subject, TDX Title/Description) ----------

function InlineEditor({
  content,
  fields,
  onChange,
}: {
  content: InlineTemplateContent;
  fields: TicketTemplateFieldDto[];
  onChange: (content: InlineTemplateContent) => void;
}) {
  const parts = content.parts;
  const setParts = (next: InlinePart[]) => onChange({ parts: next });

  const preview = parts
    .map((p) => (p.type === 'field' ? `[${fields.find((f) => f.key === p.fieldKey)?.label ?? p.fieldKey}]` : p.text ?? ''))
    .join('');

  return (
    <div>
      <div
        style={{
          padding: '8px 12px',
          background: 'var(--bg-page)',
          borderRadius: 6,
          fontSize: 13,
          fontFamily: 'var(--mono)',
          marginBottom: 10,
          color: 'var(--ink-soft)',
        }}
      >
        Aperçu : {preview || <em>(vide)</em>}
      </div>

      {parts.map((part, i) => (
        <div key={i} style={rowStyle}>
          <select
            value={part.type}
            onChange={(ev) => {
              const type = ev.target.value as 'field' | 'text';
              setParts(parts.map((p, idx) => (idx === i ? (type === 'field' ? { type, fieldKey: fields[0]?.key } : { type, text: '' }) : p)));
            }}
            style={{ width: 100 }}
          >
            <option value="field">Champ</option>
            <option value="text">Texte</option>
          </select>

          {part.type === 'field' ? (
            <select
              value={part.fieldKey ?? ''}
              onChange={(ev) => setParts(parts.map((p, idx) => (idx === i ? { ...p, fieldKey: ev.target.value } : p)))}
              style={{ flex: 1 }}
            >
              {fields.map((f) => (
                <option key={f.key} value={f.key}>
                  {f.label}
                </option>
              ))}
            </select>
          ) : (
            <input
              type="text"
              value={part.text ?? ''}
              onChange={(ev) => setParts(parts.map((p, idx) => (idx === i ? { ...p, text: ev.target.value } : p)))}
              placeholder="Texte (ex. un espace, un tiret, une parenthèse...)"
              style={{ flex: 1 }}
            />
          )}

          <MoveRemove
            onUp={i > 0 ? () => setParts(move(parts, i, i - 1)) : undefined}
            onDown={i < parts.length - 1 ? () => setParts(move(parts, i, i + 1)) : undefined}
            onRemove={() => setParts(parts.filter((_, idx) => idx !== i))}
          />
        </div>
      ))}

      <div style={{ display: 'flex', gap: 8, marginTop: 8 }}>
        <button type="button" style={addBtnStyle} onClick={() => setParts([...parts, { type: 'field', fieldKey: fields[0]?.key }])}>
          + Champ
        </button>
        <button type="button" style={addBtnStyle} onClick={() => setParts([...parts, { type: 'text', text: '' }])}>
          + Texte
        </button>
      </div>
    </div>
  );
}

// ---------- Block editor (Freshdesk ticket bodies) ----------

function EmployeeFieldsEditor({
  fields,
  employeeFields,
  onChange,
}: {
  fields: TicketTemplateFieldDto[];
  employeeFields: EmployeeFieldLine[];
  onChange: (lines: EmployeeFieldLine[]) => void;
}) {
  return (
    <div style={{ marginLeft: 24, marginTop: 6, marginBottom: 6, borderLeft: '2px solid var(--border)', paddingLeft: 12 }}>
      <div style={{ fontSize: 12, color: 'var(--muted)', marginBottom: 6 }}>
        Ces lignes se répètent automatiquement pour chaque employé visé par la demande.
      </div>
      {employeeFields.map((line, i) => (
        <div key={i} style={rowStyle}>
          <input
            type="text"
            value={line.label}
            onChange={(ev) => onChange(employeeFields.map((l, idx) => (idx === i ? { ...l, label: ev.target.value } : l)))}
            placeholder="Étiquette (ex. Poste)"
            style={{ width: 180 }}
          />
          <span style={{ fontSize: 12 }}>→</span>
          <select
            value={line.fieldKey}
            onChange={(ev) => onChange(employeeFields.map((l, idx) => (idx === i ? { ...l, fieldKey: ev.target.value } : l)))}
            style={{ flex: 1 }}
          >
            {fields.map((f) => (
              <option key={f.key} value={f.key}>
                {f.label}
              </option>
            ))}
          </select>
          <MoveRemove
            onUp={i > 0 ? () => onChange(move(employeeFields, i, i - 1)) : undefined}
            onDown={i < employeeFields.length - 1 ? () => onChange(move(employeeFields, i, i + 1)) : undefined}
            onRemove={() => onChange(employeeFields.filter((_, idx) => idx !== i))}
          />
        </div>
      ))}
      <button
        type="button"
        style={addBtnStyle}
        onClick={() => onChange([...employeeFields, { label: '', fieldKey: fields[0]?.key ?? '' }])}
      >
        + Champ employé
      </button>
    </div>
  );
}

function BlockEditor({
  content,
  requestFields,
  employeeFieldCatalog,
  onChange,
}: {
  content: BlockTemplateContent;
  requestFields: TicketTemplateFieldDto[];
  employeeFieldCatalog: TicketTemplateFieldDto[];
  onChange: (content: BlockTemplateContent) => void;
}) {
  const blocks = content.blocks;
  const setBlocks = (next: TemplateBlock[]) => onChange({ blocks: next });

  return (
    <div>
      {blocks.map((block, i) => (
        <div key={i} style={{ marginBottom: 6 }}>
          <div style={rowStyle}>
            <select
              value={block.type}
              onChange={(ev) => {
                const type = ev.target.value as TemplateBlock['type'];
                const next: TemplateBlock =
                  type === 'heading'
                    ? { type, headingText: '', employeeFields: [] }
                    : type === 'field'
                      ? { type, label: '', fieldKey: requestFields[0]?.key, employeeFields: [] }
                      : { type, employeeGroupHeading: '', employeeFields: [] };
                setBlocks(blocks.map((b, idx) => (idx === i ? next : b)));
              }}
              style={{ width: 150 }}
            >
              <option value="heading">Titre de section</option>
              <option value="field">Ligne</option>
              <option value="employeeGroup">Groupe par employé</option>
            </select>

            {block.type === 'heading' && (
              <input
                type="text"
                value={block.headingText ?? ''}
                onChange={(ev) => setBlocks(blocks.map((b, idx) => (idx === i ? { ...b, headingText: ev.target.value } : b)))}
                placeholder="Titre (ex. Détails)"
                style={{ flex: 1 }}
              />
            )}

            {block.type === 'field' && (
              <>
                <input
                  type="text"
                  value={block.label ?? ''}
                  onChange={(ev) => setBlocks(blocks.map((b, idx) => (idx === i ? { ...b, label: ev.target.value } : b)))}
                  placeholder="Étiquette (ex. Date de création)"
                  style={{ width: 220 }}
                />
                <span style={{ fontSize: 12 }}>→</span>
                <select
                  value={block.fieldKey ?? ''}
                  onChange={(ev) => setBlocks(blocks.map((b, idx) => (idx === i ? { ...b, fieldKey: ev.target.value } : b)))}
                  style={{ flex: 1 }}
                >
                  {requestFields.map((f) => (
                    <option key={f.key} value={f.key}>
                      {f.label}
                    </option>
                  ))}
                </select>
              </>
            )}

            {block.type === 'employeeGroup' && (
              <input
                type="text"
                value={block.employeeGroupHeading ?? ''}
                onChange={(ev) =>
                  setBlocks(blocks.map((b, idx) => (idx === i ? { ...b, employeeGroupHeading: ev.target.value } : b)))
                }
                placeholder="Titre de section (facultatif, ex. Employé)"
                style={{ flex: 1 }}
              />
            )}

            <MoveRemove
              onUp={i > 0 ? () => setBlocks(move(blocks, i, i - 1)) : undefined}
              onDown={i < blocks.length - 1 ? () => setBlocks(move(blocks, i, i + 1)) : undefined}
              onRemove={() => setBlocks(blocks.filter((_, idx) => idx !== i))}
            />
          </div>

          {block.type === 'employeeGroup' && (
            <EmployeeFieldsEditor
              fields={employeeFieldCatalog}
              employeeFields={block.employeeFields}
              onChange={(lines) => setBlocks(blocks.map((b, idx) => (idx === i ? { ...b, employeeFields: lines } : b)))}
            />
          )}
        </div>
      ))}

      <div style={{ display: 'flex', gap: 8, marginTop: 8 }}>
        <button type="button" style={addBtnStyle} onClick={() => setBlocks([...blocks, { type: 'heading', headingText: '', employeeFields: [] }])}>
          + Titre de section
        </button>
        <button
          type="button"
          style={addBtnStyle}
          onClick={() => setBlocks([...blocks, { type: 'field', label: '', fieldKey: requestFields[0]?.key, employeeFields: [] }])}
        >
          + Ligne
        </button>
        <button
          type="button"
          style={addBtnStyle}
          onClick={() => setBlocks([...blocks, { type: 'employeeGroup', employeeGroupHeading: '', employeeFields: [] }])}
        >
          + Groupe par employé
        </button>
      </div>
    </div>
  );
}

// ---------- Template card (shared wrapper: load/save/reset) ----------

function TemplateCard({ template, onSaved }: { template: TicketTemplateDto; onSaved: (updated: TicketTemplateDto) => void }) {
  const api = useApi();
  const [contentJson, setContentJson] = useState(template.content);
  const [isSaving, setIsSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);

  const isDirty = contentJson !== template.content;
  const isDefault = contentJson === template.defaultContent;

  const parsed = JSON.parse(contentJson) as InlineTemplateContent | BlockTemplateContent;

  const handleChange = (next: InlineTemplateContent | BlockTemplateContent) => {
    setContentJson(JSON.stringify(next));
    setSaved(false);
  };

  const handleSave = async () => {
    setSaveError(null);
    setIsSaving(true);
    try {
      const updated = await api.ticketTemplates.update(template.key, { content: contentJson });
      onSaved(updated);
      setSaved(true);
    } catch (err) {
      setSaveError(err instanceof Error ? err.message : 'Erreur inconnue');
    } finally {
      setIsSaving(false);
    }
  };

  const handleReset = () => {
    setContentJson(template.defaultContent);
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

      {template.shape === 'Inline' ? (
        <InlineEditor
          content={parsed as InlineTemplateContent}
          fields={template.requestFields.concat(template.employeeFields)}
          onChange={handleChange}
        />
      ) : (
        <BlockEditor
          content={parsed as BlockTemplateContent}
          requestFields={template.requestFields}
          employeeFieldCatalog={template.employeeFields}
          onChange={handleChange}
        />
      )}

      <div style={{ fontSize: 12, color: 'var(--muted)', marginTop: 10 }}>
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

  const onboarding = templates.filter((t) => t.key.endsWith('Onboarding'));
  const offboarding = templates.filter((t) => t.key.endsWith('Offboarding'));

  return (
    <div>
      <div className="step-panel__header" style={{ marginBottom: 16 }}>
        <div>
          <div className="step-panel__title">Gabarits des billets</div>
          <div className="step-panel__subtitle">
            Contrôle le contenu envoyé à chaque système (Freshdesk, TDX) lors de la soumission d'une demande. Chaque
            ligne affiche une étiquette suivie d'un champ de données — aucun code ni symbole à taper. Un champ vide
            au moment de la création du billet s'affiche comme « — ».
          </div>
        </div>
      </div>

      {isLoading && <div>Chargement…</div>}
      {loadError && <div className="big-notice">{loadError}</div>}

      {!isLoading && !loadError && (
        <>
          <h3 style={{ marginTop: 24 }}>Intégration / Réactivation</h3>
          {onboarding.map((t) => (
            <TemplateCard key={t.key} template={t} onSaved={handleSaved} />
          ))}

          <h3 style={{ marginTop: 24 }}>Terminaison</h3>
          {offboarding.map((t) => (
            <TemplateCard key={t.key} template={t} onSaved={handleSaved} />
          ))}
        </>
      )}
    </div>
  );
}
