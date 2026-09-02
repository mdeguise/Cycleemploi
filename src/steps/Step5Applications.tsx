import { useWizard } from '../context/WizardContext';
import { Field, SectionTitle } from '../components/FormField';
import { StepFooter } from '../components/StepFooter';
import { ChoiceCard } from '../components/ChoiceCard';
import { AppsIcon, FileTextIcon } from '../components/icons';
import { APPLICATIONS, DYNAWAY, ACCES_D365 } from '../data/catalogs';

export function Step5Applications() {
  const { request, setRequest } = useWizard();
  const apps = request.applications;

  const toggle = (id: string) => {
    setRequest((prev) => {
      const set = new Set(prev.applications.applications);
      if (set.has(id)) set.delete(id);
      else set.add(id);
      const nowSelected = set.has(id);

      // Dynaway implicitly requires D365 access — selecting it auto-checks Accès D365 in the
      // Access step; see Step3Access, which locks that checkbox while Dynaway stays selected.
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
          <AppsIcon style={{ width: 22, height: 22 }} />
        </span>
        <div>
          <div className="step-panel__title">Applications et licences</div>
          <div className="step-panel__subtitle">Sélectionnez les applications requises pour ce poste</div>
        </div>
      </div>

      <div className="choice-list">
        {APPLICATIONS.map((app) => (
          <ChoiceCard
            key={app.nom}
            title={app.nom}
            description={app.description}
            selected={apps.applications.includes(app.nom)}
            onToggle={() => toggle(app.nom)}
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
        Les licences logicielles sont attribuées selon les besoins du poste et les autorisations en vigueur. Certaines
        demandes peuvent nécessiter une approbation supplémentaire.
      </div>

      <StepFooter />
    </div>
  );
}
