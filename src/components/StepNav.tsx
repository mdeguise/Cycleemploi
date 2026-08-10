import { useState } from 'react';
import { useWizard } from '../context/WizardContext';
import { HelpModal } from './HelpModal';
import { CheckIcon, LifeBuoyIcon } from './icons';

export function StepNav() {
  const { steps, currentStep, furthestStep, goToStep } = useWizard();
  const [helpOpen, setHelpOpen] = useState(false);

  return (
    <nav className="step-nav">
      {steps.map((step, index) => {
        const isActive = index === currentStep;
        const isDone = index < furthestStep && !isActive;
        const className = [
          'step-nav__item',
          isActive ? 'step-nav__item--active' : '',
          isDone ? 'step-nav__item--done' : '',
        ]
          .filter(Boolean)
          .join(' ');

        return (
          <button key={step.key} type="button" className={className} onClick={() => goToStep(index)}>
            <span className="step-nav__icon">{isDone ? <CheckIcon style={{ width: 16, height: 16 }} /> : step.numero}</span>
            <span>
              <div className="step-nav__title">{step.titre}</div>
              <div className="step-nav__subtitle">{step.sousTitre}</div>
            </span>
          </button>
        );
      })}

      <button type="button" className="help-box" onClick={() => setHelpOpen(true)}>
        <span className="help-box__icon">
          <LifeBuoyIcon style={{ width: 18, height: 18 }} />
        </span>
        <span>
          <div className="help-box__title">Besoin d'aide?</div>
          <div className="help-box__subtitle">Communiquez avec l'équipe TI</div>
        </span>
      </button>

      <HelpModal open={helpOpen} onClose={() => setHelpOpen(false)} />
    </nav>
  );
}
