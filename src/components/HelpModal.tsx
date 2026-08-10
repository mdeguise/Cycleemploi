import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useApi } from '../api/ApiContext';
import { ApiError } from '../api/client';
import { CheckCircleIcon, LifeBuoyIcon } from './icons';

interface HelpModalProps {
  open: boolean;
  onClose: () => void;
}

export function HelpModal({ open, onClose }: HelpModalProps) {
  const api = useApi();
  const [description, setDescription] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [ticketId, setTicketId] = useState<number | null>(null);

  const { data: helpUrl } = useQuery({
    queryKey: ['help-url'],
    queryFn: () => api.auth.helpUrl(),
    staleTime: Infinity,
    retry: false,
    enabled: open,
  });

  if (!open) return null;

  const handleClose = () => {
    setDescription('');
    setError(null);
    setTicketId(null);
    onClose();
  };

  const handleSubmit = async () => {
    setError(null);
    setIsSubmitting(true);
    try {
      const result = await api.auth.createHelpTicket({ description });
      setTicketId(result.ticketId);
    } catch (err) {
      setError(
        err instanceof ApiError
          ? "Votre demande n'a pas pu être envoyée. Veuillez réessayer ou utiliser le formulaire complet ci-dessous."
          : 'Une erreur inattendue est survenue.',
      );
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="modal-overlay" onClick={handleClose}>
      <div className="modal-card help-modal-card" onClick={(ev) => ev.stopPropagation()}>
        {ticketId === null ? (
          <>
            <span className="modal-card__icon">
              <LifeBuoyIcon style={{ width: 28, height: 28 }} />
            </span>
            <div className="modal-card__title">Besoin d'aide?</div>
            <p className="modal-card__text">
              Décrivez votre problème ou votre question ci-dessous. Une demande sera envoyée directement à l'équipe
              informatique.
            </p>
            <div className="field help-modal__field">
              <label className="field__label">Description de votre problème ou de votre question</label>
              <textarea
                value={description}
                onChange={(ev) => setDescription(ev.target.value)}
                placeholder="Décrivez votre problème ou votre question…"
                rows={5}
                autoFocus
              />
            </div>
            {error && (
              <div className="required-note" style={{ color: 'var(--tremblant-red-dark)' }}>
                {error}
              </div>
            )}
            <div className="help-modal__actions">
              <button type="button" className="btn btn-secondary" onClick={handleClose}>
                Annuler
              </button>
              <button
                type="button"
                className="btn btn-primary"
                onClick={handleSubmit}
                disabled={isSubmitting || !description.trim()}
              >
                {isSubmitting ? 'Envoi en cours…' : 'Envoyer'}
              </button>
            </div>
            {helpUrl?.url && (
              <a href={helpUrl.url} target="_blank" rel="noopener noreferrer" className="help-modal__fallback-link">
                Besoin de plus d'options (priorité, pièce jointe)? Ouvrir le formulaire complet
              </a>
            )}
          </>
        ) : (
          <>
            <span className="modal-card__icon">
              <CheckCircleIcon style={{ width: 28, height: 28 }} />
            </span>
            <div className="modal-card__title">Demande envoyée</div>
            <p className="modal-card__text">
              Votre demande a été envoyée à l'équipe informatique (billet TDX #{ticketId}). Vous serez contacté
              directement pour le suivi.
            </p>
            <button type="button" className="btn btn-primary" onClick={handleClose}>
              Fermer
            </button>
          </>
        )}
      </div>
    </div>
  );
}
