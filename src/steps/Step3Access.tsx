import { Fragment } from 'react';
import { useWizard } from '../context/WizardContext';
import { Field, SectionTitle } from '../components/FormField';
import { StepFooter } from '../components/StepFooter';
import { ChoiceCard } from '../components/ChoiceCard';
import { LockIcon, AlertTriangleIcon } from '../components/icons';
import { SYSTEMES_ACCES, POS_HEBERGEMENT_SYSTEMES, ACCES_BADGE, BESOIN_CODE_ALARME } from '../data/catalogs';

export function Step3Access() {
  const { request, setRequest } = useWizard();
  const a = request.access;

  const toggleSysteme = (id: string) => {
    setRequest((prev) => {
      const set = new Set(prev.access.systemes);
      if (set.has(id)) set.delete(id);
      else set.add(id);
      return { ...prev, access: { ...prev.access, systemes: Array.from(set) } };
    });
  };

  const togglePosHebergement = (nom: string) => {
    setRequest((prev) => {
      const set = new Set(prev.access.posHebergement);
      if (set.has(nom)) set.delete(nom);
      else set.add(nom);
      return { ...prev, access: { ...prev.access, posHebergement: Array.from(set) } };
    });
  };

  const updateBadgeZones = (badgeZones: string) => {
    setRequest((prev) => ({ ...prev, access: { ...prev.access, badgeZones } }));
  };

  const updateCodeAlarmeDetails = (codeAlarmeDetails: string) => {
    setRequest((prev) => ({ ...prev, access: { ...prev.access, codeAlarmeDetails } }));
  };

  const updateJustification = (justification: string) => {
    setRequest((prev) => ({ ...prev, access: { ...prev.access, justification } }));
  };

  const updateStationnement = (stationnement: string) => {
    setRequest((prev) => ({ ...prev, access: { ...prev.access, stationnement } }));
  };

  return (
    <div className="step-panel">
      <div className="step-panel__header">
        <span className="step-panel__icon">
          <LockIcon style={{ width: 22, height: 22 }} />
        </span>
        <div>
          <div className="step-panel__title">Accès et comptes</div>
          <div className="step-panel__subtitle">
            Sélectionnez les accès et les applications dont l'employé a besoin pour exercer ses fonctions.
          </div>
        </div>
      </div>

      <div className="important-notice">
        <AlertTriangleIcon className="important-notice__icon" />
        <div>
          <strong>Important</strong> — Sélectionnez uniquement les accès nécessaires aux fonctions de l'employé. Les
          demandes d'accès sont traitées selon les autorisations et les politiques de sécurité de l'entreprise.
        </div>
      </div>

      <div className="choice-list">
        {SYSTEMES_ACCES.map((sys) => (
          <Fragment key={sys.nom}>
            <ChoiceCard
              title={sys.nom}
              description={sys.description}
              selected={a.systemes.includes(sys.nom)}
              onToggle={() => toggleSysteme(sys.nom)}
            />
            {sys.nom === ACCES_BADGE && a.systemes.includes(ACCES_BADGE) && (
              <Field label="Zones ou édifices requis">
                <input
                  type="text"
                  value={a.badgeZones}
                  onChange={(ev) => updateBadgeZones(ev.target.value)}
                  placeholder="Précisez les zones ou édifices requis"
                />
              </Field>
            )}
            {sys.nom === BESOIN_CODE_ALARME && a.systemes.includes(BESOIN_CODE_ALARME) && (
              <Field label="Précisions - code d'alarme">
                <input
                  type="text"
                  value={a.codeAlarmeDetails}
                  onChange={(ev) => updateCodeAlarmeDetails(ev.target.value)}
                  placeholder="Précisez l'emplacement ou tout détail utile pour le code d'alarme"
                />
              </Field>
            )}
          </Fragment>
        ))}
      </div>

      <SectionTitle icon={<LockIcon style={{ width: 16, height: 16 }} />}>Système POS et Hébergement</SectionTitle>
      <div className="step-panel__subtitle" style={{ marginTop: -8, marginBottom: 16 }}>
        Sélectionnez les applications requises pour ce poste
      </div>
      <div className="choice-list">
        {POS_HEBERGEMENT_SYSTEMES.map((sys) => (
          <ChoiceCard
            key={sys.nom}
            title={sys.nom}
            description={sys.description}
            badge={sys.facultatif ? 'Facultatif' : undefined}
            selected={a.posHebergement.includes(sys.nom)}
            onToggle={() => togglePosHebergement(sys.nom)}
          />
        ))}
      </div>

      <Field label="Stationnement requis">
        <input
          type="text"
          value={a.stationnement}
          onChange={(ev) => updateStationnement(ev.target.value)}
          placeholder="Sélectionnez ou précisez le ou les stationnements requis"
        />
      </Field>

      <Field label="Justification / précisions additionnelles">
        <textarea
          value={a.justification}
          onChange={(ev) => updateJustification(ev.target.value)}
          placeholder="Précisez tout accès particulier requis pour ce rôle"
        />
      </Field>

      <StepFooter />
    </div>
  );
}
