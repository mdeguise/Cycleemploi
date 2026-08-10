import { useWizard } from '../context/WizardContext';
import { FileTextIcon } from './icons';
import { formatDateFr } from '../utils/formatDate';
import { TYPE_DEMANDE_TERMINAISON } from '../types';
import staffPhoto from '../assets/staff2.png';

const STATUS_LABEL: Record<string, string> = {
  Brouillon: 'En brouillon',
  Soumise: 'Soumise',
  'En traitement': 'En traitement',
  Complétée: 'Complétée',
};

export function SummarySidebar() {
  const { request, currentStep, stepCount } = useWizard();
  const progressPct = Math.round(((currentStep + 1) / stepCount) * 100);

  return (
    <aside className="summary-sidebar">
      <div className="summary-card">
        <div className="summary-card__header">
          <FileTextIcon className="summary-card__header-icon" />
          Résumé de la demande
        </div>

        <div className="summary-row">
          <div className="summary-row__label">Type de demande</div>
          <div className="summary-row__value">{request.typeDemande || '—'}</div>
        </div>
        <div className="summary-row">
          <div className="summary-row__label">Date de création</div>
          <div className="summary-row__value">{formatDateFr(request.dateCreation)}</div>
        </div>
        {request.typeDemande === TYPE_DEMANDE_TERMINAISON
          ? request.offboarding.derniereJournee && (
              <div className="summary-row">
                <div className="summary-row__label">Dernière journée</div>
                <div className="summary-row__value">{formatDateFr(request.offboarding.derniereJournee)}</div>
              </div>
            )
          : request.employee.dateEntreePrevue && (
              <div className="summary-row">
                <div className="summary-row__label">Date d'entrée en fonction</div>
                <div className="summary-row__value">{formatDateFr(request.employee.dateEntreePrevue)}</div>
              </div>
            )}
        <div className="summary-row">
          <div className="summary-row__label">Demandé par</div>
          <div className="summary-row__value">{request.demandePar}</div>
        </div>
        <div className="summary-row">
          <div className="summary-row__label">Statut actuel</div>
          <div>
            <span className="summary-status-badge">{STATUS_LABEL[request.statut]}</span>
          </div>
        </div>
        <div className="summary-row" style={{ marginBottom: 0 }}>
          <div className="summary-row__label">Progression</div>
          <div className="summary-row__value">
            {currentStep + 1} / {stepCount} étapes
          </div>
          <div className="summary-progress-bar">
            <div className="summary-progress-bar__fill" style={{ width: `${progressPct}%` }} />
          </div>
        </div>
      </div>

      <div className="summary-photo-card">
        <img src={staffPhoto} alt="" className="summary-photo-card__img" />
      </div>
    </aside>
  );
}
