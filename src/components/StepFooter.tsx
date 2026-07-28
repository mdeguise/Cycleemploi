import { useState } from 'react';
import { useWizard } from '../context/WizardContext';
import { ChevronRightIcon, SaveIcon } from './icons';

export function StepFooter({ onSubmit, submitDisabled }: { onSubmit?: () => void; submitDisabled?: boolean }) {
  const { currentStep, stepCount, goNext, goBack, isStepValid, syncToServer, isSyncing } = useWizard();
  const isLast = currentStep === stepCount - 1;
  const canAdvance = isStepValid(currentStep);
  const [savedMessage, setSavedMessage] = useState(false);

  const handleSaveDraft = async () => {
    await syncToServer();
    setSavedMessage(true);
    setTimeout(() => setSavedMessage(false), 2000);
  };

  const handleCancel = () => {
    // Phase 1: no dedicated cancel/delete-draft endpoint (see plan) — discarding just abandons the
    // in-progress draft locally; the server-side row (if a request was already created) is left as
    // an orphaned Brouillon, which is an acceptable Phase 1 gap, not a real cleanup mechanism.
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
        <button type="button" className="btn btn-secondary" onClick={handleSaveDraft} disabled={isSyncing}>
          <SaveIcon style={{ width: 16, height: 16 }} />
          {savedMessage ? 'Enregistré ✓' : 'Enregistrer le brouillon'}
        </button>
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
