import { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useApi } from '../api/ApiContext';
import { Field } from '../components/FormField';
import type { D365AccessApprovalDetailDto, MeDto } from '../api/types';

export function D365ApprovalFormPage({ me }: { me: MeDto }) {
  const { requestId } = useParams<{ requestId: string }>();
  const navigate = useNavigate();
  const api = useApi();

  const [data, setData] = useState<D365AccessApprovalDetailDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [jobTitleEnglish, setJobTitleEnglish] = useState('');
  const [approvalLimit, setApprovalLimit] = useState('');
  const [levyEmployee, setLevyEmployee] = useState(false);
  const [apAccessDetails, setApAccessDetails] = useState('');
  const [additionalLegalEntities, setAdditionalLegalEntities] = useState('');
  const [defaultShippingAddress, setDefaultShippingAddress] = useState('');
  const [comments, setComments] = useState('');
  const [roles, setRoles] = useState<string[]>([]);

  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [submitResult, setSubmitResult] = useState<{ succeeded: boolean; ticketNumber?: string | null } | null>(null);

  useEffect(() => {
    if (!requestId) return;
    setIsLoading(true);
    setLoadError(null);
    api.d365AccessApprovals
      .detail(Number(requestId))
      .then((d) => {
        setData(d);
        setJobTitleEnglish(d.jobTitleEnglish ?? '');
        setApprovalLimit(d.approvalLimit != null ? String(d.approvalLimit) : '');
        setLevyEmployee(d.levyEmployee ?? false);
        setApAccessDetails(d.apAccessDetails ?? '');
        setAdditionalLegalEntities(d.additionalLegalEntities ?? '');
        setDefaultShippingAddress(d.defaultShippingAddress ?? '');
        setComments(d.comments ?? '');
        setRoles(d.roles);
      })
      .catch((err) => setLoadError(err instanceof Error ? err.message : 'Erreur inconnue'))
      .finally(() => setIsLoading(false));
  }, [api, requestId]);

  const toggleRole = (role: string) => {
    setRoles((prev) => (prev.includes(role) ? prev.filter((r) => r !== role) : [...prev, role]));
  };

  const handleSubmit = async (ev: React.FormEvent) => {
    ev.preventDefault();
    if (!requestId) return;
    setSubmitError(null);

    if (!jobTitleEnglish.trim()) {
      setSubmitError('Le titre du poste (anglais) est requis.');
      return;
    }
    const limit = Number(approvalLimit);
    if (approvalLimit.trim() === '' || Number.isNaN(limit) || limit < 0) {
      setSubmitError('La limite d\'approbation doit être un montant valide.');
      return;
    }

    setIsSubmitting(true);
    try {
      const result = await api.d365AccessApprovals.complete(Number(requestId), {
        jobTitleEnglish: jobTitleEnglish.trim(),
        approvalLimit: limit,
        levyEmployee,
        apAccessDetails: apAccessDetails.trim() || null,
        additionalLegalEntities: additionalLegalEntities.trim() || null,
        defaultShippingAddress: defaultShippingAddress.trim() || null,
        comments: comments.trim() || null,
        roles,
      });
      setSubmitResult(result);
      if (!result.succeeded) {
        setSubmitError(result.error ?? 'La création du billet TDX a échoué.');
      }
    } catch (err) {
      setSubmitError(err instanceof Error ? err.message : 'Erreur inconnue');
    } finally {
      setIsSubmitting(false);
    }
  };

  if (isLoading) return <div className="step-panel">Chargement…</div>;
  if (loadError) return <div className="step-panel"><div className="big-notice">{loadError}</div></div>;
  if (!data) return null;

  const readOnly = !data.canComplete || submitResult?.succeeded;

  return (
    <div className="step-panel">
      <div className="step-panel__header">
        <div>
          <div className="step-panel__title">Accès D365 — {data.employeeName}</div>
          <div className="step-panel__subtitle">Demande {data.requestNumber} — demandée par {data.requesterName}</div>
        </div>
      </div>

      {!submitResult && data.status === 'Pending' && (
        <div className="big-notice">
          En appuyant sur « Envoyer », une véritable demande d'accès D365 sera créée dans TDX (formulaire « D365 -
          Access », équipe ENT - FinApp Triage) — cette action n'est pas réversible depuis cette page.
        </div>
      )}
      {submitResult?.succeeded && (
        <div className="big-notice">
          Billet TDX créé{submitResult.ticketNumber ? ` — numéro ${submitResult.ticketNumber}` : ''}. Cette approbation est
          maintenant complétée.
        </div>
      )}
      {!data.canComplete && data.status === 'Pending' && (
        <div className="big-notice">
          {me.isD365Approver ? (
            <>
              Vous consultez cette demande, mais vous n'êtes pas l'approbateur assigné — seul un approbateur D365 associé
              (globalement ou pour le titre de poste « {data.positionTitle ?? '—'} ») peut la compléter.
            </>
          ) : (
            <>
              Vous consultez cette demande à titre de Personnel TI (accès en lecture seule) — seul un approbateur
              D365 associé peut la compléter et l'envoyer à TDX.
            </>
          )}
        </div>
      )}
      {data.status === 'Completed' && !submitResult && (
        <div className="big-notice">Cette approbation a déjà été complétée — les valeurs ci-dessous sont en lecture seule.</div>
      )}

      <div className="field-section-title">Informations connues</div>
      <div className="field-grid field-grid--2">
        <Field label="Employé"><input type="text" value={data.employeeName} disabled /></Field>
        <Field label="Courriel"><input type="text" value={data.employeeEmail ?? '—'} disabled /></Field>
        <Field label="Titre de poste (Workday)"><input type="text" value={data.positionTitle ?? '—'} disabled /></Field>
        <Field label="Code d'emploi"><input type="text" value={data.jobCode ?? '—'} disabled /></Field>
        <Field label="Département"><input type="text" value={data.departement ?? '—'} disabled /></Field>
        <Field label="Gestionnaire"><input type="text" value={data.managerName ?? '—'} disabled /></Field>
        <Field label="Date de début"><input type="text" value={data.startDate ?? '—'} disabled /></Field>
        <Field label="Demandé par"><input type="text" value={data.requesterName} disabled /></Field>
        <Field label="Resort"><input type="text" value="Tremblant" disabled /></Field>
        <Field label="Access Type"><input type="text" value="New Access" disabled /></Field>
        <Field label="Entité légale"><input type="text" value={data.legalEntity} disabled /></Field>
        <Field label="Numéro de département"><input type="text" value={data.departmentNumber ?? '—'} disabled /></Field>
      </div>

      <form onSubmit={handleSubmit}>
        <div className="field-section-title">À compléter</div>
        <div className="field-grid field-grid--2">
          <Field label="Titre du poste (anglais)" required>
            <input type="text" value={jobTitleEnglish} onChange={(ev) => setJobTitleEnglish(ev.target.value)} disabled={readOnly} />
          </Field>
          <Field label="Limite d'approbation ($)" required>
            <input type="number" min="0" step="0.01" value={approvalLimit} onChange={(ev) => setApprovalLimit(ev.target.value)} disabled={readOnly} />
          </Field>
        </div>

        <Field label="Employé assujetti à une levée (Levy Employee)">
          <select value={levyEmployee ? 'Oui' : 'Non'} onChange={(ev) => setLevyEmployee(ev.target.value === 'Oui')} disabled={readOnly}>
            <option value="Non">Non</option>
            <option value="Oui">Oui</option>
          </select>
        </Field>

        <Field label="Rôles D365 requis" required>
          <div className="choice-list">
            {data.roleCatalog.map((role) => (
              <label key={role} style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '6px 0' }}>
                <input
                  type="checkbox"
                  checked={roles.includes(role)}
                  onChange={() => toggleRole(role)}
                  disabled={readOnly}
                />
                {role}
              </label>
            ))}
          </div>
        </Field>

        <Field label="Détails d'accès aux comptes payables (optionnel)">
          <textarea value={apAccessDetails} onChange={(ev) => setApAccessDetails(ev.target.value)} disabled={readOnly} />
        </Field>

        <Field label="Entités légales additionnelles (optionnel)">
          <textarea value={additionalLegalEntities} onChange={(ev) => setAdditionalLegalEntities(ev.target.value)} disabled={readOnly} />
        </Field>

        <Field label="Adresse d'expédition par défaut (optionnel)">
          <input type="text" value={defaultShippingAddress} onChange={(ev) => setDefaultShippingAddress(ev.target.value)} disabled={readOnly} />
        </Field>

        <Field label="Détails additionnels ou commentaires (optionnel)">
          <textarea value={comments} onChange={(ev) => setComments(ev.target.value)} disabled={readOnly} />
        </Field>

        {data.peers.length > 0 && (
          <>
            <div className="field-section-title">Employés occupant un poste similaire ({data.positionTitle})</div>
            <table style={{ width: '100%', borderCollapse: 'collapse', marginBottom: 16 }}>
              <thead>
                <tr style={{ textAlign: 'left', borderBottom: '2px solid var(--border, #ddd)' }}>
                  <th style={{ padding: '8px 12px' }}>Employé</th>
                  <th style={{ padding: '8px 12px' }}>Rôles D365 actuels</th>
                </tr>
              </thead>
              <tbody>
                {data.peers.map((p) => (
                  <tr key={p.employeeId} style={{ borderBottom: '1px solid var(--border, #eee)' }}>
                    <td style={{ padding: '8px 12px' }}>{p.employeeName}</td>
                    <td style={{ padding: '8px 12px' }}>
                      {p.roles.length === 0 ? (
                        <span style={{ color: 'var(--muted)' }}>Aucun rôle D365</span>
                      ) : (
                        <div className="review-tag-list">
                          {p.roles.map((r) => (
                            <span key={r} className="review-tag">{r}</span>
                          ))}
                        </div>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </>
        )}

        {submitError && <div className="required-note" style={{ color: 'var(--tremblant-red-dark)' }}>{submitError}</div>}

        <div style={{ display: 'flex', gap: 12, marginTop: 16 }}>
          <button type="button" className="btn btn-secondary" onClick={() => navigate('/admin/d365-approvals')}>
            Retour
          </button>
          {!readOnly && (
            <button type="submit" className="btn btn-primary" disabled={isSubmitting}>
              {isSubmitting ? 'Envoi…' : 'Envoyer'}
            </button>
          )}
        </div>
      </form>
    </div>
  );
}
