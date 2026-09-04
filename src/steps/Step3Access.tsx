import { Fragment } from 'react';
import { useWizard } from '../context/WizardContext';
import { Field, SectionTitle } from '../components/FormField';
import { StepFooter } from '../components/StepFooter';
import { ChoiceCard } from '../components/ChoiceCard';
import { LockIcon, AlertTriangleIcon, AppsIcon, FileTextIcon } from '../components/icons';
import {
  SYSTEMES_ACCES,
  POS_HEBERGEMENT_SYSTEMES,
  ACCES_BADGE,
  BESOIN_CODE_ALARME,
  STATIONNEMENT_REQUIS,
  ACCES_D365,
  DYNAWAY,
  APPLICATIONS,
} from '../data/catalogs';

export function Step3Access() {
  const { request, setRequest } = useWizard();
  const a = request.access;
  const apps = request.applications;
  const dynawaySelected = apps.applications.includes(DYNAWAY);

  const toggleSysteme = (id: string) => {
    if (id === ACCES_D365 && dynawaySelected) return; // locked — see Applications section below
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

  const toggleApplication = (id: string) => {
    setRequest((prev) => {
      const set = new Set(prev.applications.applications);
      if (set.has(id)) set.delete(id);
      else set.add(id);
      const nowSelected = set.has(id);

      // Dynaway implicitly requires D365 access — selecting it auto-checks Accès D365 above,
      // which locks that checkbox while Dynaway stays selected.
      const accessSystemes = new Set(prev.access.systemes);
      if (id === DYNAWAY && nowSelected) {
        accessSystemes.add(ACCES_D365);
      }

      return {
        ...prev,
        applications: { ...prev.applications, applications: Array.from(set) },
        access: { ...prev.access, systemes: Array.from(accessSystemes) },
      };
    });
  };

  const updateAutreLogiciel = (autreLogiciel: string) => {
    setRequest((prev) => ({ ...prev, applications: { ...prev.applications, autreLogiciel } }));
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

      <div className="step-two-col">
        <div className="step-two-col__col">
          <div className="choice-list">
            {SYSTEMES_ACCES.map((sys) => (
              <Fragment key={sys.nom}>
                <ChoiceCard
                  title={sys.nom}
                  description={sys.description}
                  selected={a.systemes.includes(sys.nom)}
                  onToggle={() => toggleSysteme(sys.nom)}
                  disabled={sys.nom === ACCES_D365 && dynawaySelected}
                  disabledHint={
                    sys.nom === ACCES_D365 && dynawaySelected
                      ? 'Requis automatiquement — Dynaway est sélectionné dans Applications.'
                      : undefined
                  }
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
                {sys.nom === STATIONNEMENT_REQUIS && a.systemes.includes(STATIONNEMENT_REQUIS) && (
                  <Field label="Précisions - stationnement">
                    <input
                      type="text"
                      value={a.stationnement}
                      onChange={(ev) => updateStationnement(ev.target.value)}
                      placeholder="Sélectionnez ou précisez le ou les stationnements requis"
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

          <Field label="Justification / précisions additionnelles">
            <textarea
              value={a.justification}
              onChange={(ev) => updateJustification(ev.target.value)}
              placeholder="Précisez tout accès particulier requis pour ce rôle"
            />
          </Field>
        </div>

        <div className="step-two-col__col">
          <SectionTitle icon={<AppsIcon style={{ width: 16, height: 16 }} />}>Applications et licences</SectionTitle>
          <div className="choice-list">
            {APPLICATIONS.map((app) => (
              <ChoiceCard
                key={app.nom}
                title={app.nom}
                description={app.description}
                selected={apps.applications.includes(app.nom)}
                onToggle={() => toggleApplication(app.nom)}
              />
            ))}
          </div>

          <SectionTitle icon={<FileTextIcon style={{ width: 16, height: 16 }} />}>Autres applications requises</SectionTitle>
          <Field label="Indiquez toute autre application ou licence nécessaire qui ne figure pas dans la liste ci-dessus.">
            <textarea
              value={apps.autreLogiciel}
              onChange={(ev) => updateAutreLogiciel(ev.target.value)}
              placeholder="ex.: Foxit, Visio, Project, Power BI."
            />
          </Field>
          <div className="step-panel__subtitle" style={{ marginTop: -12 }}>
            Les licences logicielles sont attribuées selon les besoins du poste et les autorisations en vigueur.
            Certaines demandes peuvent nécessiter une approbation supplémentaire.
          </div>
        </div>
      </div>

      <StepFooter />
    </div>
  );
}
