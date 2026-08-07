import { useWizard } from '../context/WizardContext';
import { Field } from '../components/FormField';
import { StepFooter } from '../components/StepFooter';
import { ShieldIcon, LaptopIcon, LockIcon, ShirtIcon, AlertTriangleIcon } from '../components/icons';
import type { OffboardingInfo } from '../types';

export function Step3DepartmentComments() {
  const { request, setRequest } = useWizard();
  const o = request.offboarding;

  const update = (patch: Partial<OffboardingInfo>) => {
    setRequest((prev) => ({ ...prev, offboarding: { ...prev.offboarding, ...patch } }));
  };

  return (
    <div className="step-panel">
      <div className="step-panel__header">
        <span className="step-panel__icon">
          <ShieldIcon style={{ width: 22, height: 22 }} />
        </span>
        <div>
          <div className="step-panel__title">Commentaires par département</div>
          <div className="step-panel__subtitle">
            Ajoutez les informations nécessaires pour chaque équipe impliquée dans le traitement de cet avis
          </div>
        </div>
      </div>

      <div className="big-notice">
        <AlertTriangleIcon className="big-notice__icon" />
        <div className="big-notice__text">
          Afin de garantir la confidentialité, assurez-vous que vous avez la bonne case de COMMENTAIRES. Il est de
          votre responsabilité, en tant que gestionnaire, de vous assurer que la confidentialité de l'employé est
          respectée.
        </div>
      </div>

      <div className="confidential-field">
        <Field
          label={
            <span className="dept-comment-label">
              <ShieldIcon style={{ width: 15, height: 15 }} />
              Ressources humaines
              <span className="confidential-badge">Confidentiel</span>
            </span>
          }
        >
          <textarea
            value={o.commentairesRH}
            onChange={(ev) => update({ commentairesRH: ev.target.value })}
            placeholder="Commentaires destinés uniquement à l'équipe des ressources humaines"
          />
        </Field>
      </div>

      <Field
        label={
          <span className="dept-comment-label">
            <LaptopIcon style={{ width: 15, height: 15 }} />
            Technologies de l'information
          </span>
        }
      >
        <textarea
          value={o.commentairesIT}
          onChange={(ev) => update({ commentairesIT: ev.target.value })}
          placeholder="Commentaires concernant les comptes, ordinateurs ou autre matériel informatique de l'employé"
        />
      </Field>

      <Field
        label={
          <span className="dept-comment-label">
            <LockIcon style={{ width: 15, height: 15 }} />
            Stationnement
          </span>
        }
      >
        <textarea
          value={o.commentairesStationnement}
          onChange={(ev) => update({ commentairesStationnement: ev.target.value })}
          placeholder="Commentaires concernant le stationnement de l'employé"
        />
      </Field>

      <Field
        label={
          <span className="dept-comment-label">
            <LockIcon style={{ width: 15, height: 15 }} />
            Puce d'accès
          </span>
        }
      >
        <textarea
          value={o.commentairesPuceAcces}
          onChange={(ev) => update({ commentairesPuceAcces: ev.target.value })}
          placeholder="Commentaires concernant la puce d'accès de l'employé"
        />
      </Field>

      <Field
        label={
          <span className="dept-comment-label">
            <ShirtIcon style={{ width: 15, height: 15 }} />
            Redingote (vêtements et matériel à retourner)
          </span>
        }
      >
        <textarea
          value={o.commentairesRedingote}
          onChange={(ev) => update({ commentairesRedingote: ev.target.value })}
          placeholder="Précisez les vêtements ou tout autre matériel que l'employé doit retourner"
        />
      </Field>

      <StepFooter />
    </div>
  );
}
