import { useState } from 'react';
import { useWizard } from '../context/WizardContext';
import { StepFooter } from '../components/StepFooter';
import { SubmissionModal } from '../components/SubmissionModal';
import {
  CheckCircleIcon,
  UserIcon,
  BriefcaseIcon,
  LockIcon,
  LaptopIcon,
  AppsIcon,
  ShieldIcon,
  ShirtIcon,
} from '../components/icons';
import { REGLE_DE_PAYE_AUTRE, ACCES_BADGE, BESOIN_CODE_ALARME } from '../data/catalogs';
import { formatDateFr } from '../utils/formatDate';

export function Step6Review() {
  const { request, goToStep, submitRequest } = useWizard();
  const { employee: e, access: a, equipment: eq, applications: apps, comments: c } = request;
  const selected = e.employee;
  const [showConfirmation, setShowConfirmation] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  // Catalog values are stored as their own display text (see backend/Api/Controllers/
  // RequestsController.cs's multi-select junction tables) — no id-to-name lookup needed anymore,
  // the stored value IS the display name.
  const nameFor = (value: string) => value;

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
          <div className="step-panel__subtitle">Vérifiez les informations avant d'envoyer la demande</div>
        </div>
      </div>

      <div className="review-section">
        <div className="review-section__header">
          <span style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <UserIcon style={{ width: 15, height: 15 }} /> Employé
          </span>
          <span className="review-section__edit" onClick={() => goToStep(0)}>
            Modifier
          </span>
        </div>
        <div className="review-section__body">
          <div>
            <div className="review-item__label">Type de demande</div>
            <div className="review-item__value">{request.typeDemande || '—'}</div>
          </div>
          <div>
            <div className="review-item__label">Nom complet</div>
            <div className="review-item__value">{selected ? `${selected.prenom} ${selected.nom}` : '—'}</div>
          </div>
          <div>
            <div className="review-item__label">Numéro d'employé</div>
            <div className="review-item__value">{selected?.numeroEmploye || '—'}</div>
          </div>
          <div>
            <div className="review-item__label">Date d'entrée prévue</div>
            <div className="review-item__value">{e.dateEntreePrevue ? formatDateFr(e.dateEntreePrevue) : '—'}</div>
          </div>
          <div>
            <div className="review-item__label">Règle de paye</div>
            <div className="review-item__value">{e.regleDePaye || '—'}</div>
          </div>
          {e.regleDePaye === REGLE_DE_PAYE_AUTRE && (
            <div>
              <div className="review-item__label">Précisions</div>
              <div className="review-item__value">{e.regleDePayeCommentaire || '—'}</div>
            </div>
          )}
        </div>
      </div>

      <div className="review-section">
        <div className="review-section__header">
          <span style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <BriefcaseIcon style={{ width: 15, height: 15 }} /> Poste et département
          </span>
          <span className="review-section__edit" onClick={() => goToStep(1)}>
            Modifier
          </span>
        </div>
        <div className="review-section__body">
          <div>
            <div className="review-item__label">Département</div>
            <div className="review-item__value">{selected?.departement || '—'}</div>
          </div>
          <div>
            <div className="review-item__label">Poste</div>
            <div className="review-item__value">{selected?.poste || '—'}</div>
          </div>
          <div>
            <div className="review-item__label">Code d'emploi</div>
            <div className="review-item__value">{selected?.codeEmploi || '—'}</div>
          </div>
          <div>
            <div className="review-item__label">Type d'employé</div>
            <div className="review-item__value">{selected?.typeEmploi || '—'}</div>
          </div>
          <div>
            <div className="review-item__label">Gestionnaire immédiat</div>
            <div className="review-item__value">{selected?.gestionnaire || '—'}</div>
          </div>
        </div>
      </div>

      <div className="review-section">
        <div className="review-section__header">
          <span style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <LockIcon style={{ width: 15, height: 15 }} /> Accès et comptes
          </span>
          <span className="review-section__edit" onClick={() => goToStep(2)}>
            Modifier
          </span>
        </div>
        <div className="review-section__body">
          <div>
            <div className="review-item__label">Systèmes et accès</div>
            <div className="review-tag-list">
              {a.systemes.length ? (
                a.systemes.map((value) => (
                  <span key={value} className="review-tag">
                    {nameFor(value)}
                  </span>
                ))
              ) : (
                <span className="review-item__value">Aucun accès sélectionné</span>
              )}
            </div>
          </div>
          {a.systemes.includes(ACCES_BADGE) && (
            <div>
              <div className="review-item__label">Zones ou édifices requis</div>
              <div className="review-item__value">{a.badgeZones || '—'}</div>
            </div>
          )}
          {a.systemes.includes(BESOIN_CODE_ALARME) && (
            <div>
              <div className="review-item__label">Précisions - code d'alarme</div>
              <div className="review-item__value">{a.codeAlarmeDetails || '—'}</div>
            </div>
          )}
          <div>
            <div className="review-item__label">Système POS et Hébergement</div>
            <div className="review-tag-list">
              {a.posHebergement.length ? (
                a.posHebergement.map((nom) => (
                  <span key={nom} className="review-tag">
                    {nom}
                  </span>
                ))
              ) : (
                <span className="review-item__value">Aucun système sélectionné</span>
              )}
            </div>
          </div>
          <div>
            <div className="review-item__label">Stationnement requis</div>
            <div className="review-item__value">{a.stationnement || '—'}</div>
          </div>
        </div>
      </div>

      <div className="review-section">
        <div className="review-section__header">
          <span style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <LaptopIcon style={{ width: 15, height: 15 }} /> Équipement
          </span>
          <span className="review-section__edit" onClick={() => goToStep(3)}>
            Modifier
          </span>
        </div>
        <div className="review-section__body">
          <div className="review-tag-list">
            {eq.equipements.length ? (
              eq.equipements.map((value) => (
                <span key={value} className="review-tag">
                  {nameFor(value)}
                </span>
              ))
            ) : (
              <span className="review-item__value">Aucun équipement sélectionné</span>
            )}
          </div>
        </div>
      </div>

      <div className="review-section">
        <div className="review-section__header">
          <span style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <AppsIcon style={{ width: 15, height: 15 }} /> Applications
          </span>
          <span className="review-section__edit" onClick={() => goToStep(4)}>
            Modifier
          </span>
        </div>
        <div className="review-section__body">
          <div>
            <div className="review-item__label">Applications sélectionnées</div>
            <div className="review-tag-list">
              {apps.applications.length ? (
                apps.applications.map((value) => (
                  <span key={value} className="review-tag">
                    {nameFor(value)}
                  </span>
                ))
              ) : (
                <span className="review-item__value">Aucune application sélectionnée</span>
              )}
            </div>
          </div>
          <div>
            <div className="review-item__label">Autre logiciel requis</div>
            <div className="review-item__value">{apps.autreLogiciel || '—'}</div>
          </div>
        </div>
      </div>

      <div className="review-section">
        <div className="review-section__header">
          <span style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <ShieldIcon style={{ width: 15, height: 15 }} /> Commentaires et suivis
          </span>
          <span className="review-section__edit" onClick={() => goToStep(5)}>
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
            <div className="review-item__value">{c.commentairesRH || '—'}</div>
          </div>
          <div>
            <div className="review-item__label">
              <span style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                <LaptopIcon style={{ width: 12, height: 12 }} /> Technologies de l'information
              </span>
            </div>
            <div className="review-item__value">{c.commentairesIT || '—'}</div>
          </div>
          <div>
            <div className="review-item__label">
              <span style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                <LockIcon style={{ width: 12, height: 12 }} /> Stationnement
              </span>
            </div>
            <div className="review-item__value">{c.commentairesStationnement || '—'}</div>
          </div>
          <div>
            <div className="review-item__label">
              <span style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                <LockIcon style={{ width: 12, height: 12 }} /> Carte ou puce d'accès
              </span>
            </div>
            <div className="review-item__value">{c.commentairesPuceAcces || '—'}</div>
          </div>
          <div>
            <div className="review-item__label">
              <span style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                <ShirtIcon style={{ width: 12, height: 12 }} /> Uniformes et matériel à fournir
              </span>
            </div>
            <div className="review-item__value">{c.commentairesRedingote || '—'}</div>
          </div>
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
