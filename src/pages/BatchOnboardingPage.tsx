import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useApi } from '../api/ApiContext';
import { useEmployeeSearch } from '../hooks/useEmployeeSearch';
import { Field } from '../components/FormField';
import { ChoiceCard } from '../components/ChoiceCard';
import { DateInput } from '../components/DateInput';
import { RegleDePayeSelect } from '../components/RegleDePayeSelect';
import { UserIcon, SearchIcon, XIcon, ChevronDownIcon, CheckCircleIcon, AlertTriangleIcon } from '../components/icons';
import { SYSTEMES_ACCES, ACCES_D365, EQUIPEMENTS, REGLE_DE_PAYE_AUTRE, PAY_GROUP_NON_UNION } from '../data/catalogs';
import type { EmployeeDto, SubmitRequestDto } from '../api/types';
import tremblantLogo from '../assets/logo-tremblant.png';

function initials(prenom: string, nom: string) {
  return `${prenom[0] ?? ''}${nom[0] ?? ''}`.toUpperCase();
}

function toggleValue(list: string[], value: string): string[] {
  return list.includes(value) ? list.filter((v) => v !== value) : [...list, value];
}

/** Accès D365 is deliberately left out of batch mode for now — it needs its own per-person detail
 * form (type d'accès, titre du poste anglais, rôles…) that this first iteration doesn't collect.
 * A row that needs D365 should be submitted individually, or built here then "Personnalisé" after
 * the fact from the normal single-employee wizard. */
const BATCH_SYSTEMES = SYSTEMES_ACCES.filter((s) => s.nom !== ACCES_D365);

interface Profile {
  systemesAcces: string[];
  equipements: string[];
  allocationMensuelle: string;
  approuveParDirecteur: boolean;
}

const emptyProfile = (): Profile => ({ systemesAcces: [], equipements: [], allocationMensuelle: '', approuveParDirecteur: false });

interface BatchRow {
  id: string;
  employee: EmployeeDto | null;
  dateEntreePrevue: string;
  regleDePaye: string;
  regleDePayeCommentaire: string;
  /** null = this row follows the shared default profile below; non-null = it has its own. */
  customProfile: Profile | null;
}

let nextRowId = 1;
const makeRow = (): BatchRow => ({
  id: String(nextRowId++),
  employee: null,
  dateEntreePrevue: '',
  regleDePaye: '',
  regleDePayeCommentaire: '',
  customProfile: null,
});

type RowResult = { status: 'pending' | 'ok' | 'error'; requestNumber?: string; error?: string };

function ProfileEditor({ profile, onChange }: { profile: Profile; onChange: (patch: Partial<Profile>) => void }) {
  const hasAllocation = profile.allocationMensuelle.trim() !== '' && Number(profile.allocationMensuelle) > 0;
  return (
    <>
      <div className="choice-list">
        {BATCH_SYSTEMES.map((sys) => (
          <ChoiceCard
            key={sys.nom}
            title={sys.nom}
            description={sys.description}
            selected={profile.systemesAcces.includes(sys.nom)}
            onToggle={() => onChange({ systemesAcces: toggleValue(profile.systemesAcces, sys.nom) })}
            lightSelection
          />
        ))}
      </div>
      <div className="choice-list" style={{ marginTop: 12 }}>
        {EQUIPEMENTS.map((item) => (
          <ChoiceCard
            key={item.nom}
            title={item.nom}
            description={item.description}
            selected={profile.equipements.includes(item.nom)}
            onToggle={() => onChange({ equipements: toggleValue(profile.equipements, item.nom) })}
            lightSelection
          />
        ))}
      </div>
      <div className="field-grid field-grid--2" style={{ marginTop: 12 }}>
        <Field label="Allocation téléphone / mois ($)">
          <input
            type="number"
            min="0"
            step="0.01"
            value={profile.allocationMensuelle}
            onChange={(ev) => onChange({ allocationMensuelle: ev.target.value })}
            placeholder="ex. 75.00"
          />
        </Field>
      </div>
      {hasAllocation && (
        <label style={{ display: 'flex', alignItems: 'center', gap: 10, fontSize: 14, marginTop: 10 }}>
          <input
            type="checkbox"
            checked={profile.approuveParDirecteur}
            onChange={(ev) => onChange({ approuveParDirecteur: ev.target.checked })}
            style={{ width: 18, height: 18 }}
          />
          Approuvé par le directeur ou la directrice du département <span style={{ color: 'var(--tremblant-red)' }}>*</span>
        </label>
      )}
    </>
  );
}

function EmployeeCell({
  employee,
  includeTerminated,
  onSelect,
  onClear,
}: {
  employee: EmployeeDto | null;
  includeTerminated: boolean;
  onSelect: (e: EmployeeDto) => void;
  onClear: () => void;
}) {
  const [query, setQuery] = useState('');
  const { data: results = [], isFetching } = useEmployeeSearch(query, includeTerminated);

  if (employee) {
    return (
      <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
        <span className="employee-selected-card__avatar" style={{ width: 30, height: 30, fontSize: 12, flexShrink: 0 }}>
          {initials(employee.prenom, employee.nom)}
        </span>
        <div style={{ minWidth: 0 }}>
          <div style={{ fontWeight: 600, fontSize: 13, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
            {employee.prenom} {employee.nom}
          </div>
          <div style={{ fontSize: 11, color: 'var(--muted)' }}>
            #{employee.employeeId} · {employee.poste ?? '—'}
          </div>
        </div>
        <button type="button" className="employee-selected-card__change" onClick={onClear} style={{ marginLeft: 'auto', flexShrink: 0 }}>
          Changer
        </button>
      </div>
    );
  }

  return (
    <div className="employee-search" style={{ minWidth: 240 }}>
      <SearchIcon className="employee-search__icon" />
      <input
        type="text"
        value={query}
        onChange={(ev) => setQuery(ev.target.value)}
        placeholder="Numéro, nom ou prénom"
        autoComplete="off"
      />
      {query.trim() && (
        <div className="employee-results">
          {isFetching ? (
            <div className="employee-result-empty">Recherche…</div>
          ) : results.length ? (
            results.map((emp) => (
              <div
                key={emp.employeeId}
                className="employee-result-item"
                onClick={() => {
                  onSelect(emp);
                  setQuery('');
                }}
              >
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
  );
}

export function BatchOnboardingPage() {
  const api = useApi();
  const [typeDemande, setTypeDemande] = useState<'Nouvelle embauche' | 'Réactivation'>('Nouvelle embauche');
  const [defaultProfile, setDefaultProfile] = useState<Profile>(emptyProfile());
  const [rows, setRows] = useState<BatchRow[]>(() => [makeRow()]);
  const [expandedRowId, setExpandedRowId] = useState<string | null>(null);
  const [results, setResults] = useState<Record<string, RowResult>>({});
  const [isSubmitting, setIsSubmitting] = useState(false);

  const includeTerminated = typeDemande === 'Réactivation';

  const updateRow = (id: string, patch: Partial<BatchRow>) =>
    setRows((prev) => prev.map((r) => (r.id === id ? { ...r, ...patch } : r)));

  const addRow = () => setRows((prev) => [...prev, makeRow()]);
  const removeRow = (id: string) => {
    setRows((prev) => (prev.length > 1 ? prev.filter((r) => r.id !== id) : prev));
    if (expandedRowId === id) setExpandedRowId(null);
  };

  const profileFor = (row: BatchRow) => row.customProfile ?? defaultProfile;

  const toggleCustomize = (row: BatchRow) => {
    if (row.customProfile) {
      updateRow(row.id, { customProfile: null });
      if (expandedRowId === row.id) setExpandedRowId(null);
    } else {
      updateRow(row.id, { customProfile: { ...defaultProfile } });
      setExpandedRowId(row.id);
    }
  };

  const updateRowProfile = (row: BatchRow, patch: Partial<Profile>) =>
    updateRow(row.id, { customProfile: { ...profileFor(row), ...patch } });

  const isRowValid = (row: BatchRow) => {
    if (!row.employee || !row.dateEntreePrevue) return false;
    const nonRequise = row.employee.payGroup === PAY_GROUP_NON_UNION;
    if (!nonRequise && !row.regleDePaye) return false;
    if (row.regleDePaye === REGLE_DE_PAYE_AUTRE && !row.regleDePayeCommentaire.trim()) return false;
    const p = profileFor(row);
    if (p.allocationMensuelle.trim() && Number(p.allocationMensuelle) > 0 && !p.approuveParDirecteur) return false;
    return true;
  };

  const hasDuplicateEmployees = (() => {
    const ids = rows.map((r) => r.employee?.employeeId).filter(Boolean);
    return new Set(ids).size !== ids.length;
  })();

  const allValid = rows.length > 0 && rows.every(isRowValid) && !hasDuplicateEmployees;

  const buildDto = (row: BatchRow): SubmitRequestDto => {
    const p = profileFor(row);
    const e = row.employee!;
    return {
      requestType: typeDemande === 'Réactivation' ? 'Reactivation' : 'Onboarding',
      employees: [
        {
          workdayEmployeeId: e.employeeId,
          nameSnapshot: `${e.prenom} ${e.nom}`,
          positionSnapshot: e.poste ?? null,
          departementSnapshot: e.departement ?? null,
          codeEmploiSnapshot: e.codeEmploi ?? null,
          typeEmploiSnapshot: e.typeEmploi ?? null,
          gestionnaireSnapshot: e.gestionnaire ?? null,
        },
      ],
      dateEntreePrevue: row.dateEntreePrevue,
      regleDePaye: row.regleDePaye || null,
      regleDePayeCommentaire: row.regleDePayeCommentaire || null,
      systemesAcces: p.systemesAcces,
      badgeZones: null,
      codeAlarmeDetails: null,
      systemePosHebergement: [],
      stationnementRequis: null,
      justificationAcces: null,
      equipements: p.equipements,
      notesEquipement: null,
      allocationMensuelleEquipement: p.allocationMensuelle.trim() ? Number(p.allocationMensuelle) : null,
      approuveParDirecteurEquipement: p.approuveParDirecteur,
      applications: [],
      autreLogicielRequis: null,
      commentairesRH: null,
      commentairesIT: null,
      commentairesStationnement: null,
      commentairesPuceAcces: null,
      commentairesRedingote: null,
      derniereJournee: null,
      indemniteVacances: null,
      raisonArret: null,
      detailsRaison: null,
      reembaucheriez: null,
      dateRetourConnue: null,
      dateRetourTravail: null,
      preavisRecu: null,
      motifNonAdmissibilite: null,
      d365Detail: null,
    };
  };

  const submitBatch = async () => {
    setIsSubmitting(true);
    const initial: Record<string, RowResult> = {};
    rows.forEach((r) => {
      initial[r.id] = { status: 'pending' };
    });
    setResults(initial);

    // Sequential on purpose — each row fires its own Freshdesk/TDX/D365 integrations; submitting a
    // whole batch in parallel would hammer those systems all at once for no real benefit.
    for (const row of rows) {
      try {
        const created = await api.requests.submit(buildDto(row));
        setResults((prev) => ({ ...prev, [row.id]: { status: 'ok', requestNumber: created.requestNumber } }));
      } catch (err) {
        setResults((prev) => ({
          ...prev,
          [row.id]: { status: 'error', error: err instanceof Error ? err.message : 'Erreur inconnue' },
        }));
      }
    }
    setIsSubmitting(false);
  };

  const hasResults = Object.keys(results).length > 0;

  return (
    <div className="app-shell">
      <header className="app-header">
        <div className="app-header__brand">
          <img src={tremblantLogo} alt="Tremblant" className="app-header__logo" />
          <div className="app-header__title">Embauche en lot (maquette)</div>
        </div>
        <div style={{ display: 'flex', gap: 16, alignItems: 'center' }}>
          <Link to="/" className="btn btn-secondary" style={{ textDecoration: 'none' }}>
            Retour au formulaire standard
          </Link>
        </div>
      </header>

      <div className="app-body" style={{ display: 'block', maxWidth: 1100, margin: '0 auto', width: '100%', padding: '20px 24px' }}>
        <div className="step-panel">
          <div className="step-panel__header">
            <span className="step-panel__icon">
              <UserIcon style={{ width: 22, height: 22 }} />
            </span>
            <div>
              <div className="step-panel__title">Embauche en lot</div>
              <div className="step-panel__subtitle">
                Maquette — soumet une demande distincte par personne, chacune suivant le pipeline habituel
                (Freshdesk, TDX, D365). L'accès D365 n'est pas encore pris en charge en mode lot.
              </div>
            </div>
          </div>

          <div className="type-demande-grid" style={{ gridTemplateColumns: '1fr 1fr', marginBottom: 20 }}>
            <button
              type="button"
              className={`type-demande-option${typeDemande === 'Nouvelle embauche' ? ' type-demande-option--selected' : ''}`}
              onClick={() => setTypeDemande('Nouvelle embauche')}
            >
              <span className="type-demande-option__radio">
                {typeDemande === 'Nouvelle embauche' && <span className="type-demande-option__radio-dot" />}
              </span>
              <span>
                <div className="type-demande-option__title">Nouvelle embauche</div>
              </span>
            </button>
            <button
              type="button"
              className={`type-demande-option${typeDemande === 'Réactivation' ? ' type-demande-option--selected' : ''}`}
              onClick={() => setTypeDemande('Réactivation')}
            >
              <span className="type-demande-option__radio">
                {typeDemande === 'Réactivation' && <span className="type-demande-option__radio-dot" />}
              </span>
              <span>
                <div className="type-demande-option__title">Réactivation</div>
              </span>
            </button>
          </div>

          <div className="field-section-title">Profil par défaut — s'applique à tout le lot</div>
          <div className="step-panel__subtitle" style={{ marginTop: -4, marginBottom: 12 }}>
            Rempli une seule fois; chaque personne peut ensuite le personnaliser individuellement ci-dessous.
          </div>
          <ProfileEditor profile={defaultProfile} onChange={(patch) => setDefaultProfile((prev) => ({ ...prev, ...patch }))} />
        </div>

        <div className="step-panel" style={{ marginTop: 16 }}>
          <div className="field-section-title">Personnes ({rows.length})</div>

          {hasDuplicateEmployees && (
            <div className="required-note" style={{ marginBottom: 12 }}>
              <AlertTriangleIcon style={{ width: 16, height: 16, flexShrink: 0 }} />
              Le même employé apparaît plus d'une fois dans la liste.
            </div>
          )}

          <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
            {rows.map((row) => {
              const result = results[row.id];
              const isCustom = row.customProfile !== null;
              const isExpanded = expandedRowId === row.id;
              return (
                <div key={row.id} className="choice-card" style={{ cursor: 'default', flexDirection: 'column', alignItems: 'stretch', gap: 10 }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 12, flexWrap: 'wrap' }}>
                    <div style={{ flex: '1 1 260px' }}>
                      <EmployeeCell
                        employee={row.employee}
                        includeTerminated={includeTerminated}
                        onSelect={(e) => updateRow(row.id, { employee: e })}
                        onClear={() => updateRow(row.id, { employee: null })}
                      />
                    </div>
                    <div style={{ flex: '0 0 150px' }}>
                      <DateInput value={row.dateEntreePrevue} onChange={(v) => updateRow(row.id, { dateEntreePrevue: v })} />
                    </div>
                    <div style={{ flex: '1 1 220px' }}>
                      <RegleDePayeSelect
                        value={row.regleDePaye}
                        onChange={(v) => updateRow(row.id, { regleDePaye: v })}
                        disabled={row.employee?.payGroup === PAY_GROUP_NON_UNION}
                      />
                      {row.regleDePaye === REGLE_DE_PAYE_AUTRE && (
                        <input
                          type="text"
                          value={row.regleDePayeCommentaire}
                          onChange={(ev) => updateRow(row.id, { regleDePayeCommentaire: ev.target.value })}
                          placeholder="Précisez le quart de travail applicable"
                          style={{ marginTop: 6 }}
                        />
                      )}
                    </div>
                    <button
                      type="button"
                      className="review-section__edit"
                      onClick={() => toggleCustomize(row)}
                      style={{ whiteSpace: 'nowrap' }}
                    >
                      {isCustom ? 'Revenir au défaut' : 'Personnaliser'}
                    </button>
                    {isCustom && (
                      <button
                        type="button"
                        className="employee-selected-card__change"
                        onClick={() => setExpandedRowId(isExpanded ? null : row.id)}
                        title={isExpanded ? 'Masquer le profil' : 'Voir le profil personnalisé'}
                      >
                        <ChevronDownIcon style={{ width: 16, height: 16, transform: isExpanded ? 'rotate(180deg)' : undefined }} />
                      </button>
                    )}
                    {rows.length > 1 && (
                      <button
                        type="button"
                        className="employee-selected-card__change"
                        onClick={() => removeRow(row.id)}
                        title="Retirer cette personne"
                      >
                        <XIcon style={{ width: 16, height: 16 }} />
                      </button>
                    )}
                  </div>

                  <div style={{ fontSize: 12, color: 'var(--muted)' }}>
                    {isCustom ? <span className="review-tag">Profil personnalisé</span> : 'Suit le profil par défaut'}
                  </div>

                  {isCustom && isExpanded && (
                    <div style={{ borderTop: '1px solid var(--border, #eee)', paddingTop: 10 }}>
                      <ProfileEditor profile={row.customProfile!} onChange={(patch) => updateRowProfile(row, patch)} />
                    </div>
                  )}

                  {result && (
                    <div
                      style={{
                        fontSize: 13,
                        color: result.status === 'error' ? 'var(--tremblant-red-dark)' : 'var(--ink)',
                        display: 'flex',
                        alignItems: 'center',
                        gap: 6,
                      }}
                    >
                      {result.status === 'pending' && 'Soumission…'}
                      {result.status === 'ok' && (
                        <>
                          <CheckCircleIcon style={{ width: 15, height: 15, color: 'var(--tremblant-red)' }} />
                          Créée — #{result.requestNumber}
                        </>
                      )}
                      {result.status === 'error' && <>Échec — {result.error}</>}
                    </div>
                  )}
                </div>
              );
            })}
          </div>

          <button type="button" className="btn btn-secondary" onClick={addRow} style={{ marginTop: 12 }}>
            + Ajouter une personne
          </button>

          <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: 20 }}>
            <button type="button" className="btn btn-primary" disabled={!allValid || isSubmitting} onClick={submitBatch}>
              {isSubmitting ? 'Soumission en cours…' : `Soumettre le lot (${rows.length})`}
            </button>
          </div>

          {hasResults && !isSubmitting && (
            <div className="step-panel__subtitle" style={{ marginTop: 12 }}>
              {Object.values(results).filter((r) => r.status === 'ok').length} sur {rows.length} demande(s) créée(s) avec
              succès.
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
