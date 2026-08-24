import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useWizard } from '../context/WizardContext';
import { Field } from '../components/FormField';
import { StepFooter } from '../components/StepFooter';
import { UserIcon, SearchIcon, InfoIcon, AlertTriangleIcon } from '../components/icons';
import { REGLE_DE_PAYE_AUTRE, PAY_GROUP_NON_UNION } from '../data/catalogs';
import { RegleDePayeSelect } from '../components/RegleDePayeSelect';
import { DateInput } from '../components/DateInput';
import { TYPE_DEMANDE_TERMINAISON, type EmployeeSelectionInfo, type EmployeeSnapshot, type TypeDemande } from '../types';
import { useApi } from '../api/ApiContext';
import { useDebouncedValue } from '../hooks/useDebouncedValue';
import type { EmployeeDto } from '../api/types';

function initials(prenom: string, nom: string) {
  return `${prenom[0] ?? ''}${nom[0] ?? ''}`.toUpperCase();
}

function toSnapshot(e: EmployeeDto): EmployeeSnapshot {
  return {
    workdayEmployeeId: e.employeeId,
    prenom: e.prenom,
    nom: e.nom,
    numeroEmploye: e.employeeId,
    poste: e.poste ?? '',
    departement: e.departement ?? '',
    codeEmploi: e.codeEmploi ?? '',
    typeEmploi: e.typeEmploi ?? '',
    gestionnaire: e.gestionnaire ?? '',
  };
}

/** Live search against /api/employees/search (WorkdayDemographic), debounced. Shared by both the
 * onboarding single-select and offboarding multi-select branches below. */
function useEmployeeSearch(query: string) {
  const api = useApi();
  const debounced = useDebouncedValue(query.trim(), 300);
  return useQuery({
    queryKey: ['employees', 'search', debounced],
    queryFn: () => api.employees.search(debounced),
    enabled: debounced.length >= 2,
  });
}

export function Step1Employee() {
  const { request, setRequest, setTypeDemande } = useWizard();
  const e = request.employee;
  const isTermination = request.typeDemande === TYPE_DEMANDE_TERMINAISON;
  const [query, setQuery] = useState('');

  const { data: results = [], isFetching } = useEmployeeSearch(query);

  const selectedIds = new Set(request.offboarding.employees.map((emp) => emp.workdayEmployeeId));
  const filteredResults = isTermination ? results.filter((r) => !selectedIds.has(r.employeeId)) : results;

  const update = (patch: Partial<EmployeeSelectionInfo>) => {
    setRequest((prev) => ({ ...prev, employee: { ...prev.employee, ...patch } }));
  };

  const selectEmployee = (dto: EmployeeDto) => {
    update({ employee: toSnapshot(dto), employeePayGroup: dto.payGroup ?? '' });
    setQuery('');
  };

  const clearSelection = () => update({ employee: null, employeePayGroup: '' });

  const regleDePayeNonRequise = e.employeePayGroup === PAY_GROUP_NON_UNION;

  const addTerminationEmployee = (dto: EmployeeDto) => {
    setRequest((prev) => {
      if (prev.offboarding.employees.some((emp) => emp.workdayEmployeeId === dto.employeeId)) return prev;
      return {
        ...prev,
        offboarding: { ...prev.offboarding, employees: [...prev.offboarding.employees, toSnapshot(dto)] },
      };
    });
    setQuery('');
  };

  const removeTerminationEmployee = (workdayEmployeeId: string) => {
    setRequest((prev) => ({
      ...prev,
      offboarding: {
        ...prev.offboarding,
        employees: prev.offboarding.employees.filter((emp) => emp.workdayEmployeeId !== workdayEmployeeId),
      },
    }));
  };

  const handleTypeChange = (typeDemande: TypeDemande) => setTypeDemande(typeDemande);

  return (
    <div className="step-panel">
      <div className="step-panel__header">
        <span className="step-panel__icon">
          <UserIcon style={{ width: 22, height: 22 }} />
        </span>
        <div>
          <div className="step-panel__title">{isTermination ? 'Sélection des employés' : "Sélection de l'employé"}</div>
          <div className="step-panel__subtitle">
            {isTermination
              ? "Recherchez et sélectionnez un ou plusieurs employés visés par cet avis"
              : "Recherchez l'employé à intégrer par numéro d'employé, nom ou prénom"}
          </div>
        </div>
      </div>

      <div className={`type-demande-section${request.typeDemande ? '' : ' type-demande-section--unanswered'}`}>
        <Field
          label={
            <span className="type-demande-section__label">
              <span className="type-demande-section__badge">1</span>
              Commencez ici — Type de demande
            </span>
          }
          required
        >
          <div className="type-demande-grid">
          <button
            type="button"
            className={`type-demande-option${request.typeDemande === 'Nouvelle intégration' ? ' type-demande-option--selected' : ''}`}
            onClick={() => handleTypeChange('Nouvelle intégration')}
          >
            <span className="type-demande-option__radio">
              {request.typeDemande === 'Nouvelle intégration' && <span className="type-demande-option__radio-dot" />}
            </span>
            <span>
              <div className="type-demande-option__title">Nouvelle intégration</div>
              <div className="type-demande-option__desc">
                Cette personne n'a jamais occupé un emploi ou ne possède aucun dossier d'employé actif à Tremblant
              </div>
            </span>
          </button>
          <button
            type="button"
            className={`type-demande-option${request.typeDemande === 'Réactivation' ? ' type-demande-option--selected' : ''}`}
            onClick={() => handleTypeChange('Réactivation')}
          >
            <span className="type-demande-option__radio">
              {request.typeDemande === 'Réactivation' && <span className="type-demande-option__radio-dot" />}
            </span>
            <span>
              <div className="type-demande-option__title">Réactivation</div>
              <div className="type-demande-option__desc">
                Cette personne revient après une terminaison ou une mise à pied
              </div>
            </span>
          </button>
          <button
            type="button"
            className={`type-demande-option${isTermination ? ' type-demande-option--selected' : ''}`}
            onClick={() => handleTypeChange(TYPE_DEMANDE_TERMINAISON)}
          >
            <span className="type-demande-option__radio">{isTermination && <span className="type-demande-option__radio-dot" />}</span>
            <span>
              <div className="type-demande-option__title">Avis de terminaison ou mise à pied temporaire</div>
              <div className="type-demande-option__desc">
                Cette personne quitte son poste de façon permanente ou temporaire
              </div>
            </span>
          </button>
          </div>
        </Field>
      </div>

      {!isTermination && (
        <div className="workday-notice">
          <InfoIcon className="workday-notice__icon" />
          <ul>
            <li>
              Seuls les employés actifs dans Workday apparaîtront dans la liste ci-dessous. Un employé est considéré
              comme <strong>actif</strong> lorsque son dossier n'est pas associé à un statut de fin d'emploi — un
              employé en mise à pied demeure considéré actif.
            </li>
            <li>
              Si l'employé n'apparaît pas dans la liste déroulante, veuillez contacter votre partenaire d'affaires RH
              afin de faire activer le dossier de l'employé dans Workday.
            </li>
          </ul>
        </div>
      )}

      {!isTermination && (
        <div className="big-notice">
          <AlertTriangleIcon className="big-notice__icon" />
          <div className="big-notice__text">
            Veuillez noter que l'équipe informatique ne peut créer les comptes utilisateurs informatiques, incluant le
            compte AD, qu'à partir de 48 heures avant la première journée de travail de l'employé, selon la date de
            début du poste principal indiquée dans Workday. Il est donc essentiel que la date de la première journée
            de travail soit <strong>entrée dans Workday</strong> avant que l'équipe informatique puisse créer le
            compte.
          </div>
        </div>
      )}

      {isTermination ? (
        <>
          <div className="workday-notice">
            <InfoIcon className="workday-notice__icon" />
            <div className="workday-notice__text">
              <p>
                Vous pouvez sélectionner plusieurs employés pour cet avis. Recherchez chaque employé par numéro
                d'employé, nom ou prénom, puis cliquez sur son nom dans la liste de résultats pour l'ajouter. Répétez
                l'opération pour chaque employé additionnel visé par le même avis.
              </p>
              <p>Vous pouvez retirer un employé de la sélection en cliquant sur « Retirer » à côté de son nom.</p>
            </div>
          </div>

          <Field label="Rechercher des employés" required>
            <div className="employee-search">
              <SearchIcon className="employee-search__icon" />
              <input
                type="text"
                value={query}
                onChange={(ev) => setQuery(ev.target.value)}
                placeholder="Numéro d'employé, nom ou prénom"
                autoComplete="off"
              />
              {query.trim() && (
                <div className="employee-results">
                  {isFetching ? (
                    <div className="employee-result-empty">Recherche…</div>
                  ) : filteredResults.length ? (
                    filteredResults.map((emp) => (
                      <div key={emp.employeeId} className="employee-result-item" onClick={() => addTerminationEmployee(emp)}>
                        <span className="employee-result-item__avatar">{initials(emp.prenom, emp.nom)}</span>
                        <span>
                          <div className="employee-result-item__name">
                            {emp.prenom} {emp.nom}
                          </div>
                          <div className="employee-result-item__meta">
                            #{emp.employeeId} · {emp.poste} · {emp.departement}
                          </div>
                        </span>
                      </div>
                    ))
                  ) : (
                    <div className="employee-result-empty">Aucun employé ne correspond à cette recherche.</div>
                  )}
                </div>
              )}
            </div>
          </Field>

          {request.offboarding.employees.length > 0 && (
            <div className="choice-list">
              {request.offboarding.employees.map((emp) => (
                <div key={emp.workdayEmployeeId} className="employee-selected-card">
                  <span className="employee-selected-card__avatar">{initials(emp.prenom, emp.nom)}</span>
                  <span>
                    <div className="employee-selected-card__name">
                      {emp.prenom} {emp.nom}
                    </div>
                    <div className="employee-selected-card__meta">
                      #{emp.numeroEmploye} · {emp.poste} · {emp.departement}
                    </div>
                  </span>
                  <button
                    type="button"
                    className="employee-selected-card__change"
                    onClick={() => removeTerminationEmployee(emp.workdayEmployeeId)}
                  >
                    Retirer
                  </button>
                </div>
              ))}
            </div>
          )}
        </>
      ) : (
        <>
          {!e.employee && (
            <Field label="Rechercher un employé" required>
              <div className="employee-search">
                <SearchIcon className="employee-search__icon" />
                <input
                  type="text"
                  value={query}
                  onChange={(ev) => setQuery(ev.target.value)}
                  placeholder="Numéro d'employé, nom ou prénom"
                  autoComplete="off"
                />
                {query.trim() && (
                  <div className="employee-results">
                    {isFetching ? (
                      <div className="employee-result-empty">Recherche…</div>
                    ) : results.length ? (
                      results.map((emp) => (
                        <div key={emp.employeeId} className="employee-result-item" onClick={() => selectEmployee(emp)}>
                          <span className="employee-result-item__avatar">{initials(emp.prenom, emp.nom)}</span>
                          <span>
                            <div className="employee-result-item__name">
                              {emp.prenom} {emp.nom}
                            </div>
                            <div className="employee-result-item__meta">
                              #{emp.employeeId} · {emp.poste} · {emp.departement}
                            </div>
                          </span>
                        </div>
                      ))
                    ) : (
                      <div className="employee-result-empty">Aucun employé ne correspond à cette recherche.</div>
                    )}
                  </div>
                )}
              </div>
            </Field>
          )}

          {e.employee && (
            <div className="employee-selected-card">
              <span className="employee-selected-card__avatar">{initials(e.employee.prenom, e.employee.nom)}</span>
              <span>
                <div className="employee-selected-card__name">
                  {e.employee.prenom} {e.employee.nom}
                </div>
                <div className="employee-selected-card__meta">
                  #{e.employee.numeroEmploye} · {e.employee.poste} · {e.employee.departement}
                </div>
              </span>
              <button type="button" className="employee-selected-card__change" onClick={clearSelection}>
                Changer
              </button>
            </div>
          )}

          <div className="field-grid field-grid--2">
            <Field label="Date d'entrée prévue" required valid={Boolean(e.dateEntreePrevue)}>
              <DateInput value={e.dateEntreePrevue} onChange={(dateEntreePrevue) => update({ dateEntreePrevue })} />
            </Field>
            <Field label="Règle de paye" required={!regleDePayeNonRequise} valid={regleDePayeNonRequise || Boolean(e.regleDePaye)}>
              <RegleDePayeSelect
                value={e.regleDePaye}
                onChange={(regleDePaye) => update({ regleDePaye })}
                disabled={regleDePayeNonRequise}
              />
              {regleDePayeNonRequise && (
                <div className="field-hint">Non requis pour le groupe de paye CAN Tremblant-Non Union.</div>
              )}
            </Field>
          </div>

          {e.regleDePaye === REGLE_DE_PAYE_AUTRE && (
            <Field label="Précisez la règle de paye" required valid={Boolean(e.regleDePayeCommentaire)}>
              <input
                type="text"
                value={e.regleDePayeCommentaire}
                onChange={(ev) => update({ regleDePayeCommentaire: ev.target.value })}
                placeholder="Précisez la règle de paye applicable"
              />
            </Field>
          )}
        </>
      )}

      <div className="required-note">
        <InfoIcon style={{ width: 16, height: 16, flexShrink: 0 }} />
        Les champs marqués d'un * sont obligatoires.
      </div>

      <StepFooter />
    </div>
  );
}
