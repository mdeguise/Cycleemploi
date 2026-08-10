import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useApi } from '../api/ApiContext';

export function D365JobCodeTemplateEditPage() {
  const { jobCode = '' } = useParams<{ jobCode: string }>();
  const api = useApi();
  const navigate = useNavigate();

  const [positionTitle, setPositionTitle] = useState<string | null>(null);
  const [roleCatalog, setRoleCatalog] = useState<string[]>([]);
  const [jobTitleEnglish, setJobTitleEnglish] = useState('');
  const [legalEntity, setLegalEntity] = useState('');
  const [departmentNumber, setDepartmentNumber] = useState('');
  const [approvalLimit, setApprovalLimit] = useState('0');
  const [levyEmployee, setLevyEmployee] = useState(false);
  const [apAccessDetails, setApAccessDetails] = useState('');
  const [additionalLegalEntities, setAdditionalLegalEntities] = useState('');
  const [roles, setRoles] = useState<string[]>([]);

  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => {
    setIsLoading(true);
    setLoadError(null);
    Promise.all([api.d365JobCodeTemplates.get(jobCode), api.d365JobCodeTemplates.catalog()])
      .then(([template, catalog]) => {
        setPositionTitle(template.positionTitle ?? null);
        setJobTitleEnglish(template.jobTitleEnglish);
        setLegalEntity(template.legalEntity);
        setDepartmentNumber(template.departmentNumber);
        setApprovalLimit(String(template.approvalLimit));
        setLevyEmployee(template.levyEmployee);
        setApAccessDetails(template.apAccessDetails ?? '');
        setAdditionalLegalEntities(template.additionalLegalEntities ?? '');
        setRoles(template.roles);
        setRoleCatalog(catalog);
      })
      .catch((err) => setLoadError(err instanceof Error ? err.message : 'Erreur inconnue'))
      .finally(() => setIsLoading(false));
  }, [api, jobCode]);

  const toggleRole = (role: string) => {
    setRoles((current) => (current.includes(role) ? current.filter((r) => r !== role) : [...current, role]));
  };

  const handleSave = async (ev: React.FormEvent) => {
    ev.preventDefault();
    setSaveError(null);

    if (!jobTitleEnglish.trim() || !legalEntity.trim() || !departmentNumber.trim()) {
      setSaveError("Le titre du poste (en anglais), l'entité légale et le numéro de département sont requis.");
      return;
    }
    const limit = Number(approvalLimit);
    if (Number.isNaN(limit) || limit < 0) {
      setSaveError('La limite d\'approbation doit être un nombre positif.');
      return;
    }

    setIsSaving(true);
    try {
      await api.d365JobCodeTemplates.upsert(jobCode, {
        jobTitleEnglish: jobTitleEnglish.trim(),
        legalEntity: legalEntity.trim(),
        departmentNumber: departmentNumber.trim(),
        approvalLimit: limit,
        levyEmployee,
        apAccessDetails: apAccessDetails.trim() || null,
        additionalLegalEntities: additionalLegalEntities.trim() || null,
        roles,
      });
      navigate('/admin/d365-jobcode-templates');
    } catch (err) {
      setSaveError(err instanceof Error ? err.message : 'Erreur inconnue');
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <div className="step-panel">
      <div className="step-panel__header">
        <div>
          <div className="step-panel__title">
            Formulaire D365 — {jobCode} {positionTitle ? `(${positionTitle})` : ''}
          </div>
          <div className="step-panel__subtitle">
            Réponses au billet TDX « D365 - Access » pour ce code d'emploi. Le billet TDX doit être entièrement en
            anglais — tous les champs texte ci-dessous (titre du poste, détails d'accès AP, etc.) doivent être
            saisis en anglais.
          </div>
        </div>
      </div>

      {isLoading && <div>Chargement…</div>}
      {loadError && <div className="big-notice">{loadError}</div>}

      {!isLoading && !loadError && (
        <form onSubmit={handleSave} style={{ display: 'flex', flexDirection: 'column', gap: 16, maxWidth: 640 }}>
          <div className="field">
            <label className="field__label">Titre du poste (en anglais) *</label>
            <div className="field__input-wrap">
              <input
                type="text"
                value={jobTitleEnglish}
                onChange={(ev) => setJobTitleEnglish(ev.target.value)}
                placeholder="Ex. Accounting Clerk I"
              />
            </div>
          </div>

          <div className="field">
            <label className="field__label">Entité légale *</label>
            <div className="field__input-wrap">
              <input type="text" value={legalEntity} onChange={(ev) => setLegalEntity(ev.target.value)} />
            </div>
          </div>

          <div className="field">
            <label className="field__label">Numéro de département *</label>
            <div className="field__input-wrap">
              <input type="text" value={departmentNumber} onChange={(ev) => setDepartmentNumber(ev.target.value)} />
            </div>
          </div>

          <div className="field">
            <label className="field__label">Limite d'approbation *</label>
            <div className="field__input-wrap">
              <input
                type="number"
                min="0"
                step="0.01"
                value={approvalLimit}
                onChange={(ev) => setApprovalLimit(ev.target.value)}
                style={{ width: 160 }}
              />
            </div>
          </div>

          <div className="field">
            <label className="field__label">Employé Levy *</label>
            <div className="field__input-wrap">
              <select
                value={levyEmployee ? 'yes' : 'no'}
                onChange={(ev) => setLevyEmployee(ev.target.value === 'yes')}
                style={{ width: 160 }}
              >
                <option value="no">Non</option>
                <option value="yes">Oui</option>
              </select>
            </div>
          </div>

          <div className="field">
            <label className="field__label">Rôles D365 (billet TDX)</label>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
              {roleCatalog.map((role) => (
                <label key={role} style={{ display: 'flex', alignItems: 'center', gap: 8, fontWeight: 400 }}>
                  <input type="checkbox" checked={roles.includes(role)} onChange={() => toggleRole(role)} />
                  {role}
                </label>
              ))}
            </div>
          </div>

          <div className="field">
            <label className="field__label">Détails d'accès AP (en anglais)</label>
            <div className="field__input-wrap">
              <textarea
                value={apAccessDetails}
                onChange={(ev) => setApAccessDetails(ev.target.value)}
                rows={3}
                placeholder="Ex. mirror setup of an existing account, access details required"
              />
            </div>
          </div>

          <div className="field">
            <label className="field__label">Entités légales additionnelles (en anglais)</label>
            <div className="field__input-wrap">
              <textarea
                value={additionalLegalEntities}
                onChange={(ev) => setAdditionalLegalEntities(ev.target.value)}
                rows={2}
              />
            </div>
          </div>

          {saveError && <div className="big-notice">{saveError}</div>}

          <div style={{ display: 'flex', gap: 12 }}>
            <button type="submit" className="btn btn-primary" disabled={isSaving}>
              Enregistrer
            </button>
            <Link to="/admin/d365-jobcode-templates" className="btn btn-secondary">
              Annuler
            </Link>
          </div>
        </form>
      )}
    </div>
  );
}
