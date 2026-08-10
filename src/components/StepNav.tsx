import { useQuery } from '@tanstack/react-query';
import { useWizard } from '../context/WizardContext';
import { useApi } from '../api/ApiContext';
import { CheckIcon, LifeBuoyIcon } from './icons';

const FALLBACK_HELP_URL = 'https://form.jotform.com/260824989239069';

export function StepNav() {
  const { steps, currentStep, furthestStep, goToStep } = useWizard();
  const api = useApi();
  const { data: helpUrl } = useQuery({
    queryKey: ['help-url'],
    queryFn: () => api.auth.helpUrl(),
    staleTime: Infinity,
    retry: false,
  });

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

      <a className="help-box" href={helpUrl?.url ?? FALLBACK_HELP_URL} target="_blank" rel="noopener noreferrer">
        <span className="help-box__icon">
          <LifeBuoyIcon style={{ width: 18, height: 18 }} />
        </span>
        <span>
          <div className="help-box__title">Besoin d'aide?</div>
          <div className="help-box__subtitle">Communiquez avec l'équipe TI</div>
        </span>
      </a>
    </nav>
  );
}
