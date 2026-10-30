import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useApi } from '../api/ApiContext';
import { useWizard } from '../context/WizardContext';
import type { EmployeeSelectionInfo, EmployeeSnapshot } from '../types';
import { Field, SectionTitle } from '../components/FormField';
import { StepFooter } from '../components/StepFooter';
import { BriefcaseIcon, InfoIcon } from '../components/icons';

// Workday's raw worker-type string, translated for display — not an exhaustive catalog (Workday's
// own values aren't enumerated anywhere in this app), so an unrecognized value falls back to the
// raw text rather than showing a wrong translation. Extend this map if another raw value surfaces.
const TYPE_EMPLOI_LABELS: Record<string, string> = {
  'Full time — Employee': 'Employé à temps plein',
  'Full time - Employee': 'Employé à temps plein',
  'Part time — Employee': 'Employé à temps partiel',
  'Part time - Employee': 'Employé à temps partiel',
};
const translateTypeEmploi = (value: string) => TYPE_EMPLOI_LABELS[value] ?? value;

export function Step2Position() {
  const { request, setRequest } = useWizard();
  const api = useApi();
  const sel = request.employee;
  const selected = sel.employee;
  const isReactivation = request.typeDemande === 'Réactivation';
  const demandePoste = isReactivation && sel.changePoste === 'Oui';

  // Réactivation: Workday often still shows the PREVIOUS primary position when the manager submits (RH corrects it
  // afterwards). So the manager says whether the person changes position; when so, they
  // pick the new one here (type a few letters) and the IT and RH tickets show it.
  const { data: postes = [] } = useQuery({
    queryKey: ['employees', 'positions'],
    queryFn: () => api.employees.positions(),
    enabled: demandePoste,
    staleTime: 10 * 60 * 1000,
  });
  const [saisi, setSaisi] = useState<string | null>(null);

  const update = (patch: Partial<EmployeeSelectionInfo>) =>
    setRequest((prev) => ({ ...prev, employee: { ...prev.employee, ...patch } }));

  /** Back to the position Workday shows — the "same position" case, and an unlisted/erased title. */
  const withWorkdayPosition = (emp: EmployeeSnapshot): EmployeeSnapshot => ({
    ...emp,
    poste: emp.posteWorkday ?? emp.poste,
    codeEmploi: emp.codeEmploiWorkday ?? emp.codeEmploi,
    departement: emp.departementWorkday ?? emp.departement,
  });

  const repondre = (valeur: 'Oui' | 'Non') => {
    setSaisi(null);
    update({ changePoste: valeur, ...(selected ? { employee: withWorkdayPosition(selected) } : {}) });
  };

  // Free text: the Workday list only suggests. A title from the list brings its job code and department; any other
  // title is kept as typed, with no job code (the old one belongs to the old position) — RH confirms it in Workday.
  const choisirPoste = (valeur: string) => {
    if (!selected) return;
    setSaisi(valeur);
    const texte = valeur.trim();
    const trouve = postes.find((p) => p.poste.toLowerCase() === texte.toLowerCase());
    if (!texte || texte.toLowerCase() === (selected.posteWorkday ?? '').trim().toLowerCase()) {
      update({ employee: withWorkdayPosition(selected) });
      return;
    }
    update({
      employee: trouve
        ? {
            ...selected,
            poste: trouve.poste,
            codeEmploi: trouve.codeEmploi ?? selected.codeEmploiWorkday ?? selected.codeEmploi,
            departement: trouve.departement ?? selected.departementWorkday ?? selected.departement,
          }
        : {
            ...selected,
            poste: texte,
            codeEmploi: '',
            departement: selected.departementWorkday ?? selected.departement,
          },
    });
  };

  const posteChoisi = !!selected && selected.poste !== selected.posteWorkday;
  // Section 1 always shows what Workday has today — the poste de retour chosen below is not mixed into it.
  const actuel = {
    poste: selected?.posteWorkday ?? selected?.poste ?? '',
    codeEmploi: selected?.codeEmploiWorkday ?? selected?.codeEmploi ?? '',
    departement: selected?.departementWorkday ?? selected?.departement ?? '',
  };
  const texteSaisi = saisi ?? (posteChoisi ? selected!.poste : '');
  const saisiInvalide = texteSaisi.trim() !== '' && !posteChoisi;

  return (
    <div className="step-panel">
      <div className="step-panel__header">
        <span className="step-panel__icon">
          <BriefcaseIcon style={{ width: 22, height: 22 }} />
        </span>
        <div>
          <div className="step-panel__title">Poste et département</div>
          <div className="step-panel__subtitle">Informations provenant du dossier Workday de l'employé sélectionné</div>
        </div>
      </div>

      {!selected ? (
        <div className="required-note">
          <InfoIcon style={{ width: 16, height: 16, flexShrink: 0 }} />
          Veuillez d'abord sélectionner un employé à l'étape 1.
        </div>
      ) : (
        <>
          <SectionTitle icon={<BriefcaseIcon style={{ width: 16, height: 16 }} />}>
            {isReactivation ? 'Poste actuel (Workday)' : 'Poste actuel'}
          </SectionTitle>

          <div className="field-grid field-grid--2">
            <Field label="Département">
              <input type="text" value={actuel.departement} disabled />
            </Field>
            <Field label="Poste">
              <input type="text" value={actuel.poste} disabled />
            </Field>
          </div>

          <div className="field-grid field-grid--2">
            <Field label="Code d'emploi">
              <input type="text" value={actuel.codeEmploi} disabled />
            </Field>
            <Field label="Type d'employé">
              <input type="text" value={translateTypeEmploi(selected.typeEmploi)} disabled />
            </Field>
          </div>

          <Field label="Gestionnaire immédiat">
            <input type="text" value={selected.gestionnaire} disabled />
          </Field>

          {isReactivation && (
            <div
              className={`type-demande-section${sel.changePoste === '' || (demandePoste && !posteChoisi) ? ' type-demande-section--unanswered' : ''}`}
              style={{ marginTop: 22 }}
            >
              <SectionTitle icon={<BriefcaseIcon style={{ width: 16, height: 16 }} />}>Poste de retour</SectionTitle>
              <Field
                label={<strong style={{ fontSize: 15 }}>Est-ce que l'employé change du poste mentionné ci-haut?</strong>}
                required
              >
                <div className="type-demande-grid">
                  {(['Oui', 'Non'] as const).map((choix) => (
                    <button
                      key={choix}
                      type="button"
                      className={`type-demande-option${sel.changePoste === choix ? ' type-demande-option--selected' : ''}`}
                      onClick={() => repondre(choix)}
                    >
                      <span className="type-demande-option__radio">
                        {sel.changePoste === choix && <span className="type-demande-option__radio-dot" />}
                      </span>
                      <span className="type-demande-option__title">{choix}</span>
                    </button>
                  ))}
                </div>
              </Field>

              {demandePoste && (
                <Field label="À quel poste l'employé va-t-il revenir?" required valid={posteChoisi}>
                  <input
                    type="text"
                    list="postes-retour"
                    value={texteSaisi}
                    onChange={(ev) => choisirPoste(ev.target.value)}
                    placeholder="Entrez le poste (quelques lettres pour obtenir les suggestions de Workday)"
                    autoComplete="off"
                  />
                  <datalist id="postes-retour">
                    {postes.map((p) => (
                      <option key={p.poste} value={p.poste} />
                    ))}
                  </datalist>
                  <div className="field-hint">
                    {saisiInvalide ? (
                      <>Indiquez un poste différent de celui de Workday.</>
                    ) : (
                      <>Le poste indiqué sera transmis aux équipes TI et RH, qui le confirmeront dans Workday.</>
                    )}
                  </div>
                </Field>
              )}
            </div>
          )}
        </>
      )}

      <StepFooter />
    </div>
  );
}
