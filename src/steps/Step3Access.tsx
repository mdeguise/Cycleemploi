import { useWizard } from '../context/WizardContext';
import { Field, SectionTitle } from '../components/FormField';
import { StepFooter } from '../components/StepFooter';
import { ChoiceCard } from '../components/ChoiceCard';
import { LockIcon } from '../components/icons';
import { SYSTEMES_ACCES, POS_HEBERGEMENT_SYSTEMES, ACCES_BADGE } from '../data/catalogs';

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

  const toggleBesoinCodeAlarme = () => {
    setRequest((prev) => ({
      ...prev,
      access: { ...prev.access, besoinCodeAlarme: !prev.access.besoinCodeAlarme },
    }));
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
          <div className="step-panel__subtitle">Sélectionnez les systèmes et accès requis pour ce poste</div>
        </div>
      </div>

      <div className="choice-list">
        {SYSTEMES_ACCES.map((sys) => (
          <ChoiceCard
            key={sys.nom}
            title={sys.nom}
            description={sys.description}
            selected={a.systemes.includes(sys.nom)}
            onToggle={() => toggleSysteme(sys.nom)}
          />
        ))}
      </div>

      {a.systemes.includes(ACCES_BADGE) && (
        <>
          <Field label="Zones ou édifices requis">
            <input
              type="text"
              value={a.badgeZones}
              onChange={(ev) => updateBadgeZones(ev.target.value)}
              placeholder="Précisez les zones ou édifices requis"
            />
          </Field>
          <ChoiceCard
            title="Besoin de code d'alarme"
            description="Cochez si un code d'alarme doit être créé pour cet employé"
            selected={a.besoinCodeAlarme}
            onToggle={toggleBesoinCodeAlarme}
          />
        </>
      )}

      <SectionTitle icon={<LockIcon style={{ width: 16, height: 16 }} />}>Système POS et Hébergement</SectionTitle>
      <div className="choice-list">
        {POS_HEBERGEMENT_SYSTEMES.map((nom) => (
          <ChoiceCard
            key={nom}
            title={nom}
            selected={a.posHebergement.includes(nom)}
            onToggle={() => togglePosHebergement(nom)}
          />
        ))}
      </div>

      <Field label="Stationnement requis">
        <input
          type="text"
          value={a.stationnement}
          onChange={(ev) => updateStationnement(ev.target.value)}
          placeholder="Indiquez quel stationnement"
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
