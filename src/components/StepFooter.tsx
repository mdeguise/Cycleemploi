import { useWizard } from '../context/WizardContext';
import { ChevronRightIcon } from './icons';

export function StepFooter({ onSubmit, submitDisabled }: { onSubmit?: () => void; submitDisabled?: boolean }) {
  const { currentStep, stepCount, goNext, goBack, isStepValid } = useWizard();
  const isLast = currentStep === stepCount - 1;
  const canAdvance = isStepValid(currentStep);

  const handleCancel = () => {
    // Nothing is ever saved to the server before the final Soumettre — no partial-save state, so
    // discarding here just clears local state. Nothing to clean up server-side.
    if (window.confirm('Voulez-vous vraiment annuler cette demande? Les données non enregistrées seront perdues.')) {
      window.location.reload();
    }
  };

  return (
    <div className="step-footer">
      <button type="button" className="btn btn-secondary" onClick={handleCancel}>
        Annuler
      </button>
      <div className="step-footer__right">
        {currentStep > 0 && (
          <button type="button" className="btn btn-secondary" onClick={goBack}>
            Précédent
          </button>
        )}
        {isLast ? (
          <button type="button" className="btn btn-primary" onClick={onSubmit} disabled={submitDisabled}>
            Soumettre la demande
          </button>
        ) : (
          <button type="button" className="btn btn-primary" onClick={goNext} disabled={!canAdvance}>
            Suivant
            <ChevronRightIcon style={{ width: 16, height: 16 }} />
          </button>
        )}
      </div>
    </div>
  );
}
