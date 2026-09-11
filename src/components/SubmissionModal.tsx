import { CheckCircleIcon } from './icons';

interface SubmissionModalProps {
  open: boolean;
  onClose: () => void;
}

export function SubmissionModal({ open, onClose }: SubmissionModalProps) {
  if (!open) return null;

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-card" onClick={(ev) => ev.stopPropagation()}>
        <span className="modal-card__icon">
          <CheckCircleIcon style={{ width: 28, height: 28 }} />
        </span>
        <div className="modal-card__title">Demande soumise</div>
        <p className="modal-card__text">
          Votre demande a été soumise et vous devriez recevoir un courriel avec votre numéro de demande. Vous pourrez
          effectuer le suivi de votre demande à l'aide de ce numéro auprès des Ressources humaines dans Freshdesk ou
          de l'équipe informatique dans TDX.
        </p>
        <button type="button" className="btn btn-primary" onClick={onClose}>
          Faire une nouvelle demande
        </button>
      </div>
    </div>
  );
}
