import { useWizard } from '../context/WizardContext';
import { Field } from '../components/FormField';
import { StepFooter } from '../components/StepFooter';
import { LogOutIcon, InfoIcon, AlertTriangleIcon, PaperclipIcon, XIcon } from '../components/icons';
import {
  OUI_NON,
  RAISONS_ARRET,
  REEMBAUCHERIEZ_OPTIONS,
  RAISON_ARRET_MISE_A_PIED_TEMPORAIRE,
  RAISON_ARRET_DEMISSION_VOLONTAIRE,
  REEMBAUCHERIEZ_NON,
} from '../data/catalogs';
import { formatFileSize } from '../utils/formatFileSize';
import { DateInput } from '../components/DateInput';
import type { OffboardingInfo } from '../types';

export function Step2Cessation() {
  const { request, setRequest } = useWizard();
  const o = request.offboarding;

  const update = (patch: Partial<OffboardingInfo>) => {
    setRequest((prev) => ({ ...prev, offboarding: { ...prev.offboarding, ...patch } }));
  };

  const addFiles = (fileList: FileList | null) => {
    if (!fileList || fileList.length === 0) return;
    const newFiles = Array.from(fileList);
    setRequest((prev) => ({
      ...prev,
      offboarding: { ...prev.offboarding, attachments: [...prev.offboarding.attachments, ...newFiles] },
    }));
  };

  const removeFile = (index: number) => {
    setRequest((prev) => ({
      ...prev,
      offboarding: { ...prev.offboarding, attachments: prev.offboarding.attachments.filter((_, i) => i !== index) },
    }));
  };

  return (
    <div className="step-panel">
      <div className="step-panel__header">
        <span className="step-panel__icon">
          <LogOutIcon style={{ width: 22, height: 22 }} />
        </span>
        <div>
          <div className="step-panel__title">Détails de la fin d'emploi ou de la mise à pied</div>
          <div className="step-panel__subtitle">Précisez les informations relatives à l'arrêt de travail</div>
        </div>
      </div>

      {o.employees.length > 1 && (
        <div className="big-notice">
          <AlertTriangleIcon className="big-notice__icon" />
          <div className="big-notice__text">
            Vous avez sélectionné plusieurs employés pour cet avis de cessation d'emploi. Les réponses aux prochaines
            questions s'appliqueront donc de la même façon à tous les employés sélectionnés. Si une réponse doit être
            différente pour l'un des employés, veuillez soumettre une demande distincte uniquement pour cet employé.
          </div>
        </div>
      )}

      <Field label="Quelle est la dernière journée de travail de l'employé" required valid={Boolean(o.derniereJournee)}>
        <DateInput value={o.derniereJournee} onChange={(derniereJournee) => update({ derniereJournee })} />
      </Field>

      <Field
        label="L'équipier souhaite-t-il recevoir le paiement de ses vacances accumulées lors de sa mise à pied?"
        required
        valid={Boolean(o.indemniteVacances)}
      >
        <select value={o.indemniteVacances} onChange={(ev) => update({ indemniteVacances: ev.target.value })}>
          <option value="">Sélectionner</option>
          {OUI_NON.map((v) => (
            <option key={v} value={v}>
              {v}
            </option>
          ))}
        </select>
      </Field>

      <Field label="Motif de la fin d'emploi ou de la mise à pied" required valid={Boolean(o.raisonArret)}>
        <select value={o.raisonArret} onChange={(ev) => update({ raisonArret: ev.target.value })}>
          <option value="">Sélectionner</option>
          {RAISONS_ARRET.map((r) => (
            <option key={r} value={r}>
              {r}
            </option>
          ))}
        </select>
      </Field>

      {o.raisonArret === RAISON_ARRET_MISE_A_PIED_TEMPORAIRE && (
        <>
          <Field label="La date de retour au travail est-elle connue?" valid={Boolean(o.dateRetourConnue)}>
            <select value={o.dateRetourConnue} onChange={(ev) => update({ dateRetourConnue: ev.target.value })}>
              <option value="">Sélectionner</option>
              {OUI_NON.map((v) => (
                <option key={v} value={v}>
                  {v}
                </option>
              ))}
            </select>
          </Field>
          {o.dateRetourConnue === 'Oui' && (
            <Field label="Date prévue de retour au travail" valid={Boolean(o.dateRetourTravail)}>
              <DateInput value={o.dateRetourTravail} onChange={(dateRetourTravail) => update({ dateRetourTravail })} />
            </Field>
          )}
        </>
      )}

      {o.raisonArret === RAISON_ARRET_DEMISSION_VOLONTAIRE && (
        <Field label="Préavis reçu?" valid={Boolean(o.preavisRecu)}>
          <select value={o.preavisRecu} onChange={(ev) => update({ preavisRecu: ev.target.value })}>
            <option value="">Sélectionner</option>
            {OUI_NON.map((v) => (
              <option key={v} value={v}>
                {v}
              </option>
            ))}
          </select>
        </Field>
      )}

      <Field label="Précisions sur le motif (réponse obligatoire)" required valid={Boolean(o.detailsRaison)}>
        <textarea
          value={o.detailsRaison}
          onChange={(ev) => update({ detailsRaison: ev.target.value })}
          placeholder="Précisez les détails de la raison"
        />
        <div className="field-hint">Fournissez les informations pertinentes permettant de comprendre le contexte de la demande.</div>
      </Field>

      <Field label="Cet équipier est-il admissible à une réembauche?" required valid={Boolean(o.reembaucheriez)}>
        <select value={o.reembaucheriez} onChange={(ev) => update({ reembaucheriez: ev.target.value })}>
          <option value="">Sélectionner</option>
          {REEMBAUCHERIEZ_OPTIONS.map((v) => (
            <option key={v} value={v}>
              {v}
            </option>
          ))}
        </select>
      </Field>

      {o.reembaucheriez === REEMBAUCHERIEZ_NON && (
        <Field label="Motif de non-admissibilité à la réembauche" valid={Boolean(o.motifNonAdmissibilite)}>
          <textarea
            value={o.motifNonAdmissibilite}
            onChange={(ev) => update({ motifNonAdmissibilite: ev.target.value })}
            placeholder="Précisez le motif de non-admissibilité"
          />
        </Field>
      )}

      <Field label="Documents justificatifs (facultatif)">
        <label className="file-upload__dropzone">
          <PaperclipIcon style={{ width: 18, height: 18 }} />
          Cliquez pour joindre un ou plusieurs documents
          <input
            type="file"
            multiple
            onChange={(ev) => {
              addFiles(ev.target.files);
              ev.target.value = '';
            }}
          />
        </label>
        <div className="field-hint">Lettre de démission, entente de départ, document RH ou tout autre fichier pertinent.</div>
        {o.attachments.length > 0 && (
          <div className="file-list">
            {o.attachments.map((file, index) => (
              <div key={`${file.name}-${index}`} className="file-list-item">
                <PaperclipIcon className="file-list-item__icon" />
                <span className="file-list-item__name">{file.name}</span>
                <span className="file-list-item__size">{formatFileSize(file.size)}</span>
                <button
                  type="button"
                  className="file-list-item__remove"
                  onClick={() => removeFile(index)}
                  aria-label="Retirer le fichier"
                >
                  <XIcon style={{ width: 14, height: 14 }} />
                </button>
              </div>
            ))}
          </div>
        )}
      </Field>

      <div className="required-note">
        <InfoIcon style={{ width: 16, height: 16, flexShrink: 0 }} />
        Les champs marqués d'un * sont obligatoires.
      </div>

      <StepFooter />
    </div>
  );
}
