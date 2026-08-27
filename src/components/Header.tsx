import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { useApi } from '../api/ApiContext';
import { useWizard } from '../context/WizardContext';
import { CalendarIcon, UserIcon, ChevronDownIcon } from './icons';
import { formatDateFr } from '../utils/formatDate';
import tremblantLogo from '../assets/logo-tremblant.png';
import { TYPE_DEMANDE_TERMINAISON } from '../types';

const STATUS_LABEL: Record<string, string> = {
  Brouillon: 'En brouillon',
  Soumise: 'Soumise',
  'En traitement': 'En traitement',
  Complétée: 'Complétée',
};

export function Header() {
  const { request } = useWizard();
  const api = useApi();

  // Same query key AuthenticatedApp already loaded, so this reads from the React Query cache
  // rather than issuing a second /api/auth/me on every render of the header.
  const { data: me } = useQuery({ queryKey: ['me'], queryFn: () => api.auth.me() });
  const isTermination = request.typeDemande === TYPE_DEMANDE_TERMINAISON;

  const dateLabel = isTermination ? 'Dernière journée' : "Date d'entrée";
  const dateRaw = isTermination ? request.offboarding.derniereJournee : request.employee.dateEntreePrevue;
  const dateValue = dateRaw ? formatDateFr(dateRaw) : '—';

  return (
    <header className="app-header">
      <div className="app-header__brand">
        <img src={tremblantLogo} alt="Tremblant" className="app-header__logo" />
        <div className="app-header__title">Gestion du cycle d'emploi – Intégration, réactivation et cessation</div>
      </div>

      <div className="app-header__meta">
        <div className="meta-block">
          <CalendarIcon className="meta-block__icon" />
          <div>
            <div className="meta-block__label">{dateLabel}</div>
            <div className="meta-block__value meta-block__value--accent">{dateValue}</div>
          </div>
        </div>
        <div className="meta-block">
          <UserIcon className="meta-block__icon" />
          <div>
            <div className="meta-block__label">Demandeur</div>
            <div className="meta-block__value">{request.demandePar}</div>
          </div>
        </div>
        {/* Only rendered for someone who actually has Administration access — for everyone else
            the section does not exist, and the API refuses it regardless of what the UI shows. */}
        {me?.adminRole && (
          <Link
            to="/admin/requests"
            className="btn btn-secondary"
            style={{ whiteSpace: 'nowrap', textDecoration: 'none' }}
            title="Consulter les demandes et les billets créés"
          >
            Administration
          </Link>
        )}

        <div className="status-pill-wrap">
          <div className="status-pill-wrap__label">Statut</div>
          <div className="status-pill">
            <span className="status-dot" />
            {STATUS_LABEL[request.statut]}
            <ChevronDownIcon className="meta-block__icon" style={{ width: 14, height: 14 }} />
          </div>
        </div>
      </div>
    </header>
  );
}
