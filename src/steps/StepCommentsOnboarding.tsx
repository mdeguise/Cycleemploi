import { useWizard } from '../context/WizardContext';
import { Field } from '../components/FormField';
import { StepFooter } from '../components/StepFooter';
import { ShieldIcon, LaptopIcon, LockIcon, ShirtIcon, AlertTriangleIcon, InfoIcon } from '../components/icons';
import type { CommentsInfo } from '../types';

export function StepCommentsOnboarding() {
  const { request, setRequest } = useWizard();
  const c = request.comments;

  const update = (patch: Partial<CommentsInfo>) => {
    setRequest((prev) => ({ ...prev, comments: { ...prev.comments, ...patch } }));
  };

  return (
    <div className="step-panel">
      <div className="step-panel__header">
        <span className="step-panel__icon">
          <ShieldIcon style={{ width: 22, height: 22 }} />
        </span>
        <div>
          <div className="step-panel__title">Commentaires et suivis</div>
          <div className="step-panel__subtitle">
            Ajoutez les informations nécessaires pour chaque équipe impliquée dans le traitement de cette demande
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
              <span className="confidential-badge">
                <LockIcon style={{ width: 10, height: 10 }} />
                Visible uniquement par les ressources humaines
              </span>
            </span>
          }
        >
          <textarea
            value={c.commentairesRH}
            onChange={(ev) => update({ commentairesRH: ev.target.value })}
            placeholder="Commentaires destinés uniquement à l'équipe des ressources humaines"
          />
          <div className="field-hint">
            Assurez-vous d'inscrire vos commentaires dans la section appropriée. Les renseignements saisis ici sont
            accessibles uniquement à l'équipe des ressources humaines. Il est de votre responsabilité de protéger la
            confidentialité des renseignements personnels de l'employé. Ex. : entente particulière, condition
            d'embauche, suivi RH requis.
          </div>
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
          value={c.commentairesIT}
          onChange={(ev) => update({ commentairesIT: ev.target.value })}
          placeholder="Commentaires concernant les comptes, ordinateurs ou autre matériel informatique à préparer"
        />
        <div className="field-hint">Ex. : compte à créer en priorité, ordinateur ou logiciel spécifique requis, accès temporaire.</div>
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
          value={c.commentairesStationnement}
          onChange={(ev) => update({ commentairesStationnement: ev.target.value })}
          placeholder="Commentaires concernant le stationnement de l'employé"
        />
        <div className="field-hint">Ex. : permis à émettre, vignette à préparer, stationnement réservé.</div>
      </Field>

      <Field
        label={
          <span className="dept-comment-label">
            <LockIcon style={{ width: 15, height: 15 }} />
            Carte ou puce d'accès
          </span>
        }
      >
        <textarea
          value={c.commentairesPuceAcces}
          onChange={(ev) => update({ commentairesPuceAcces: ev.target.value })}
          placeholder="Commentaires concernant la puce d'accès de l'employé"
        />
        <div className="field-hint">Ex. : carte à préparer, zones d'accès particulières à activer.</div>
      </Field>

      <Field
        label={
          <span className="dept-comment-label">
            <ShirtIcon style={{ width: 15, height: 15 }} />
            Uniformes et matériel à fournir
          </span>
        }
      >
        <textarea
          value={c.commentairesRedingote}
          onChange={(ev) => update({ commentairesRedingote: ev.target.value })}
          placeholder="Précisez les vêtements ou tout autre matériel à fournir à l'employé"
        />
        <div className="field-hint">Ex. : manteau corporatif, uniforme, radio, outils ou autre matériel à préparer pour l'employé.</div>
      </Field>

      <div className="workday-notice">
        <InfoIcon className="workday-notice__icon" />
        <div className="workday-notice__text">
          Les commentaires saisis dans cette section serviront aux équipes concernées pour coordonner la préparation
          des accès, du matériel et des suivis administratifs.
        </div>
      </div>

      <StepFooter />
    </div>
  );
}
