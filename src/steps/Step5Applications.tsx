import { useWizard } from '../context/WizardContext';
import { Field } from '../components/FormField';
import { StepFooter } from '../components/StepFooter';
import { ChoiceCard } from '../components/ChoiceCard';
import { AppsIcon } from '../components/icons';
import { APPLICATIONS } from '../data/catalogs';

export function Step5Applications() {
  const { request, setRequest } = useWizard();
  const apps = request.applications;

  const toggle = (id: string) => {
    setRequest((prev) => {
      const set = new Set(prev.applications.applications);
      if (set.has(id)) set.delete(id);
      else set.add(id);
      return { ...prev, applications: { ...prev.applications, applications: Array.from(set) } };
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
          <div className="step-panel__subtitle">Sélectionnez les logiciels requis pour ce poste</div>
        </div>
      </div>

      <div className="choice-list">
        {APPLICATIONS.map((app) => (
          <ChoiceCard
            key={app.nom}
            title={app.nom}
            description={app.editeur}
            selected={apps.applications.includes(app.nom)}
            onToggle={() => toggle(app.nom)}
          />
        ))}
      </div>

      <Field label="Spécifiez tout autre logiciel requis pour le poste de l'employé (selon disponibilité et approbation)">
        <textarea
          value={apps.autreLogiciel}
          onChange={(ev) => updateAutreLogiciel(ev.target.value)}
          placeholder="Nommez tout autre logiciel requis"
        />
      </Field>

      <StepFooter />
    </div>
  );
}
