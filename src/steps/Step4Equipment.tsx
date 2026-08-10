import { useWizard } from '../context/WizardContext';
import { Field, SectionTitle } from '../components/FormField';
import { StepFooter } from '../components/StepFooter';
import { ChoiceCard } from '../components/ChoiceCard';
import { LaptopIcon, InfoIcon } from '../components/icons';
import { EQUIPEMENTS, CATEGORIE_TELECOMMUNICATIONS_DESCRIPTION } from '../data/catalogs';

export function Step4Equipment() {
  const { request, setRequest } = useWizard();
  const eq = request.equipment;

  const toggle = (id: string) => {
    setRequest((prev) => {
      const set = new Set(prev.equipment.equipements);
      if (set.has(id)) set.delete(id);
      else set.add(id);
      return { ...prev, equipment: { ...prev.equipment, equipements: Array.from(set) } };
    });
  };

  const updateNotes = (notes: string) => {
    setRequest((prev) => ({ ...prev, equipment: { ...prev.equipment, notes } }));
  };

  const categories = Array.from(new Set(EQUIPEMENTS.map((e) => e.categorie)));

  return (
    <div className="step-panel">
      <div className="step-panel__header">
        <span className="step-panel__icon">
          <LaptopIcon style={{ width: 22, height: 22 }} />
        </span>
        <div>
          <div className="step-panel__title">Équipement requis</div>
          <div className="step-panel__subtitle">
            Sélectionnez l'équipement nécessaire à l'employé selon les fonctions qui lui sont confiées.
          </div>
        </div>
      </div>

      <div className="workday-notice">
        <InfoIcon className="workday-notice__icon" />
        <div className="workday-notice__text">
          <p>
            Lorsque vous soumettez une demande d'équipement, veuillez noter que celle-ci fera l'objet d'une vérification
            en fonction des besoins et des exigences associés au poste de l'employé.
          </p>
          <p>
            La demande pourra également être évaluée selon la disponibilité d'équipement existant, notamment lorsqu'un
            appareil peut être réattribué à la suite du départ ou du remplacement d'un employé.
          </p>
          <p>
            La soumission d'une demande ne garantit donc pas automatiquement que l'équipement sera accordé. Chaque
            demande devra suivre le processus d'analyse et d'approbation prévu.
          </p>
        </div>
      </div>

      {categories.map((cat) => (
        <div key={cat} style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
          <SectionTitle icon={<LaptopIcon style={{ width: 16, height: 16 }} />}>
            {cat}
            {cat === 'Télécommunications' && (
              <span style={{ marginLeft: 8, fontWeight: 400, color: 'var(--muted)', fontSize: 13 }}>
                — {CATEGORIE_TELECOMMUNICATIONS_DESCRIPTION}
              </span>
            )}
          </SectionTitle>
          <div className="choice-list">
            {EQUIPEMENTS.filter((e) => e.categorie === cat).map((item) => (
              <ChoiceCard
                key={item.nom}
                title={item.nom}
                description={item.description}
                selected={eq.equipements.includes(item.nom)}
                onToggle={() => toggle(item.nom)}
              />
            ))}
          </div>
        </div>
      ))}

      <Field label="Précision sur l'équipement demandé">
        <textarea
          value={eq.notes}
          onChange={(ev) => updateNotes(ev.target.value)}
          placeholder="ex.: taille d'uniforme, besoins ergonomiques, équipement spécialisé ou toute autre précision pertinente."
        />
      </Field>

      <StepFooter />
    </div>
  );
}
