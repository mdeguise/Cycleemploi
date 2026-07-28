import { useWizard } from '../context/WizardContext';
import { Field } from '../components/FormField';
import { StepFooter } from '../components/StepFooter';
import { BriefcaseIcon, InfoIcon } from '../components/icons';

export function Step2Position() {
  const { request } = useWizard();
  const selected = request.employee.employee;

  return (
    <div className="step-panel">
      <div className="step-panel__header">
        <span className="step-panel__icon">
          <BriefcaseIcon style={{ width: 22, height: 22 }} />
        </span>
        <div>
          <div className="step-panel__title">Poste et département</div>
          <div className="step-panel__subtitle">Informations tirées du dossier de l'employé sélectionné</div>
        </div>
      </div>

      {!selected ? (
        <div className="required-note">
          <InfoIcon style={{ width: 16, height: 16, flexShrink: 0 }} />
          Veuillez d'abord sélectionner un employé à l'étape 1.
        </div>
      ) : (
        <>
          <div className="field-grid field-grid--2">
            <Field label="Département">
              <input type="text" value={selected.departement} disabled />
            </Field>
            <Field label="Poste">
              <input type="text" value={selected.poste} disabled />
            </Field>
          </div>

          <div className="field-grid field-grid--2">
            <Field label="Code d'emploi">
              <input type="text" value={selected.codeEmploi} disabled />
            </Field>
            <Field label="Type d'employé">
              <input type="text" value={selected.typeEmploi} disabled />
            </Field>
          </div>

          <Field label="Gestionnaire immédiat">
            <input type="text" value={selected.gestionnaire} disabled />
          </Field>
        </>
      )}

      <StepFooter />
    </div>
  );
}
