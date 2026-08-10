import { useState } from 'react';
import { useWizard } from '../context/WizardContext';
import { StepFooter } from '../components/StepFooter';
import { SubmissionModal } from '../components/SubmissionModal';
import {
  CheckCircleIcon,
  UserIcon,
  LogOutIcon,
  ShieldIcon,
  LaptopIcon,
  LockIcon,
  ShirtIcon,
  AlertTriangleIcon,
} from '../components/icons';
import { formatDateFr } from '../utils/formatDate';
import {
  RAISON_ARRET_MISE_A_PIED_TEMPORAIRE,
  RAISON_ARRET_DEMISSION_VOLONTAIRE,
  REEMBAUCHERIEZ_NON,
} from '../data/catalogs';

export function StepReviewOffboarding() {
  const { request, goToStep, submitRequest } = useWizard();
  const o = request.offboarding;
  const employees = o.employees;
  const [showConfirmation, setShowConfirmation] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  const handleSubmit = async () => {
    setSubmitError(null);
    setIsSubmitting(true);
    try {
      await submitRequest();
      setShowConfirmation(true);
    } catch (err) {
      setSubmitError(err instanceof Error ? err.message : 'La soumission a échoué. Veuillez réessayer.');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="step-panel">
      <div className="step-panel__header">
        <span className="step-panel__icon">
          <CheckCircleIcon style={{ width: 22, height: 22 }} />
        </span>
        <div>
          <div className="step-panel__title">Révision et soumission</div>
          <div className="step-panel__subtitle">Vérifiez les informations avant d'envoyer l'avis</div>
        </div>
      </div>

      <div className="review-section">
        <div className="review-section__header">
          <span style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <UserIcon style={{ width: 15, height: 15 }} /> Employé(s) visé(s)
          </span>
          <span className="review-section__edit" onClick={() => goToStep(0)}>
            Modifier
          </span>
        </div>
        <div className="review-section__body">
          <div className="review-tag-list">
            {employees.length ? (
              employees.map((emp) => (
                <span key={emp.workdayEmployeeId} className="review-tag">
                  {emp.prenom} {emp.nom}
                </span>
              ))
            ) : (
              <span className="review-item__value">Aucun employé sélectionné</span>
            )}
          </div>
        </div>
      </div>

      <div className="review-section">
        <div className="review-section__header">
          <span style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <LogOutIcon style={{ width: 15, height: 15 }} /> Détails de la cessation
          </span>
          <span className="review-section__edit" onClick={() => goToStep(1)}>
            Modifier
          </span>
        </div>
        <div className="review-section__body">
          <div>
            <div className="review-item__label">Dernière journée de travail</div>
            <div className="review-item__value">{o.derniereJournee ? formatDateFr(o.derniereJournee) : '—'}</div>
          </div>
          <div>
            <div className="review-item__label">Indemnité de vacances au moment de la mise à pied</div>
            <div className="review-item__value">{o.indemniteVacances || '—'}</div>
          </div>
          <div>
            <div className="review-item__label">Raison de l'arrêt de travail</div>
            <div className="review-item__value">{o.raisonArret || '—'}</div>
          </div>
          {o.raisonArret === RAISON_ARRET_MISE_A_PIED_TEMPORAIRE && (
            <>
              <div>
                <div className="review-item__label">Date de retour au travail connue?</div>
                <div className="review-item__value">{o.dateRetourConnue || '—'}</div>
              </div>
              {o.dateRetourConnue === 'Oui' && (
                <div>
                  <div className="review-item__label">Date prévue de retour au travail</div>
                  <div className="review-item__value">{o.dateRetourTravail ? formatDateFr(o.dateRetourTravail) : '—'}</div>
                </div>
              )}
            </>
          )}
          {o.raisonArret === RAISON_ARRET_DEMISSION_VOLONTAIRE && (
            <div>
              <div className="review-item__label">Préavis reçu?</div>
              <div className="review-item__value">{o.preavisRecu || '—'}</div>
            </div>
          )}
          <div>
            <div className="review-item__label">Détails de la raison</div>
            <div className="review-item__value">{o.detailsRaison || '—'}</div>
          </div>
          <div>
            <div className="review-item__label">Réembaucheriez-vous cet équipier?</div>
            <div className="review-item__value">{o.reembaucheriez || '—'}</div>
          </div>
          {o.reembaucheriez === REEMBAUCHERIEZ_NON && (
            <div>
              <div className="review-item__label">Motif de non-admissibilité à la réembauche</div>
              <div className="review-item__value">{o.motifNonAdmissibilite || '—'}</div>
            </div>
          )}
          <div>
            <div className="review-item__label">Pièces jointes</div>
            <div className="review-tag-list">
              {o.attachments.length ? (
                o.attachments.map((file, index) => (
                  <span key={`${file.name}-${index}`} className="review-tag">
                    {file.name}
                  </span>
                ))
              ) : (
                <span className="review-item__value">Aucune pièce jointe</span>
              )}
            </div>
          </div>
        </div>
      </div>

      <div className="review-section">
        <div className="review-section__header">
          <span style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <ShieldIcon style={{ width: 15, height: 15 }} /> Commentaires et suivis
          </span>
          <span className="review-section__edit" onClick={() => goToStep(2)}>
            Modifier
          </span>
        </div>
        <div className="review-section__body">
          <div>
            <div className="review-item__label">
              <span style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                Ressources humaines
                <span className="confidential-badge">
                  <LockIcon style={{ width: 10, height: 10 }} />
                  Visible uniquement par les ressources humaines
                </span>
              </span>
            </div>
            <div className="review-item__value">{o.commentairesRH || '—'}</div>
          </div>
          <div>
            <div className="review-item__label">
              <span style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                <LaptopIcon style={{ width: 12, height: 12 }} /> Technologies de l'information
              </span>
            </div>
            <div className="review-item__value">{o.commentairesIT || '—'}</div>
          </div>
          <div>
            <div className="review-item__label">
              <span style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                <LockIcon style={{ width: 12, height: 12 }} /> Stationnement
              </span>
            </div>
            <div className="review-item__value">{o.commentairesStationnement || '—'}</div>
          </div>
          <div>
            <div className="review-item__label">
              <span style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                <LockIcon style={{ width: 12, height: 12 }} /> Carte ou puce d'accès
              </span>
            </div>
            <div className="review-item__value">{o.commentairesPuceAcces || '—'}</div>
          </div>
          <div>
            <div className="review-item__label">
              <span style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                <ShirtIcon style={{ width: 12, height: 12 }} /> Uniformes et matériel à récupérer
              </span>
            </div>
            <div className="review-item__value">{o.commentairesRedingote || '—'}</div>
          </div>
        </div>
      </div>

      <div className="important-notice">
        <AlertTriangleIcon className="important-notice__icon" />
        <div>
          <strong>Important</strong> — La transmission de cet avis déclenche les processus de désactivation des
          accès, de récupération du matériel et les interventions requises par les équipes concernées. Assurez-vous
          que les renseignements fournis sont exacts et complets.
        </div>
      </div>

      {submitError && (
        <div className="required-note" style={{ color: 'var(--tremblant-red-dark)' }}>
          {submitError}
        </div>
      )}

      <StepFooter onSubmit={handleSubmit} submitDisabled={isSubmitting} />
      <SubmissionModal open={showConfirmation} onClose={() => setShowConfirmation(false)} />
    </div>
  );
}
