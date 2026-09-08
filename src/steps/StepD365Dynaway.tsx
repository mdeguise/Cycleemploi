import { useEffect, useState } from 'react';
import { useWizard } from '../context/WizardContext';
import { useApi } from '../api/ApiContext';
import { Field, SectionTitle } from '../components/FormField';
import { StepFooter } from '../components/StepFooter';
import { ChoiceCard } from '../components/ChoiceCard';
import { usePicker, PickerField } from '../components/AdPicker';
import { GridIcon, LockIcon, AlertTriangleIcon } from '../components/icons';
import { ACCES_D365, DYNAWAY, DYNAWAY_COMMENT_TAG } from '../data/catalogs';
import type { D365AdHocPrefillDto } from '../api/types';

/** Same grouping the standalone D365AccessRequest app uses for the real Microsoft Forms "D365 -
 * Access" role catalog — kept in sync with D365AdHocRequestPage.tsx's own ROLE_GROUPS. */
const ROLE_GROUPS: { title: string; prefix: string }[] = [
  { title: 'Accès Procurement (cochez tout ce qui s\'applique)', prefix: 'Procurement' },
  { title: 'Accès comptes payables (Accounts Payable)', prefix: 'Accounts Payable' },
  { title: 'Accès grand livre (General Ledger)', prefix: 'General Ledger' },
  { title: 'Rapports financiers (Financial Reporting)', prefix: 'Financial Reporting' },
  { title: 'Accès comptes recevables (Accounts Receivable)', prefix: 'Accounts Receivable' },
];

const ACCESS_TYPE_LABELS: Record<string, string> = {
  'New Access': 'Nouvel accès (New Access)',
  'Change Access': 'Modification d\'accès (Change Access)',
  'Remove Access': 'Retrait d\'accès (Remove Access)',
};

/** Everyone gets the small catalog; a short allowlist (mirrored server-side — see
 * D365AccessApprovalsController.ElevatedApprovalLimitEmails) gets the full one, same catalog the
 * standalone D365AccessRequest app uses. UI-only convenience: the backend re-checks the submitted
 * value against the same two catalogs. */
const STANDARD_APPROVAL_LIMITS = [0, 2000, 5000];
const ELEVATED_APPROVAL_LIMITS = [0, 2000, 5000, 25000, 50000, 100000, 500000, 1000000, 1500000];
const ELEVATED_APPROVAL_LIMIT_EMAILS = ['mbessette@tremblant.ca'];

function formatLimit(v: number): string {
  return v === 0 ? 'Aucune' : `${v.toLocaleString('fr-CA')} $`;
}

export function StepD365Dynaway() {
  const { request, setRequest, meEmail } = useWizard();
  const api = useApi();
  const employee = request.employee.employee;
  const d365Selected = request.access.systemes.includes(ACCES_D365);
  const dynawaySelected = request.applications.applications.includes(DYNAWAY);
  const d = request.d365;

  const managerPicker = usePicker((q) => api.d365AdHoc.adSearch(q));

  const [prefill, setPrefill] = useState<D365AdHocPrefillDto | null>(null);
  const [prefillLoadedFor, setPrefillLoadedFor] = useState<string | null>(null);
  const [isLoadingPrefill, setIsLoadingPrefill] = useState(false);
  const [prefillError, setPrefillError] = useState<string | null>(null);
  const [costCenters, setCostCenters] = useState<string[]>([]);

  const approvalLimitOptions = ELEVATED_APPROVAL_LIMIT_EMAILS.includes((meEmail ?? '').toLowerCase())
    ? ELEVATED_APPROVAL_LIMITS
    : STANDARD_APPROVAL_LIMITS;

  useEffect(() => {
    if (!d365Selected || !employee || prefillLoadedFor === employee.workdayEmployeeId) return;
    setIsLoadingPrefill(true);
    setPrefillError(null);
    api.d365AdHoc
      .prefill(employee.workdayEmployeeId)
      .then((p) => {
        setPrefill(p);
        setPrefillLoadedFor(employee.workdayEmployeeId);
        setRequest((prev) => ({
          ...prev,
          d365: {
            ...prev.d365,
            jobTitleEnglish: prev.d365.jobTitleEnglish || p.jobTitleEnglishSuggestion || '',
            departmentNumber: prev.d365.departmentNumber || p.departmentNumber || '',
          },
        }));
        if (!managerPicker.picked && !managerPicker.query && employee.gestionnaire) {
          managerPicker.setPicked({ sam: '', displayName: employee.gestionnaire, email: null });
        }
      })
      .catch((err) => setPrefillError(err instanceof Error ? err.message : 'Erreur inconnue'))
      .finally(() => setIsLoadingPrefill(false));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [d365Selected, employee?.workdayEmployeeId]);

  useEffect(() => {
    api.d365AdHoc.costCenters().then(setCostCenters).catch(() => {});
  }, [api]);

  const toggleD365 = () => {
    if (dynawaySelected) return; // locked — Dynaway implies Accès D365, see toggleDynaway
    setRequest((prev) => {
      const set = new Set(prev.access.systemes);
      if (set.has(ACCES_D365)) set.delete(ACCES_D365);
      else set.add(ACCES_D365);
      return { ...prev, access: { ...prev.access, systemes: Array.from(set) } };
    });
  };

  const toggleDynaway = () => {
    setRequest((prev) => {
      const apps = new Set(prev.applications.applications);
      const nowSelected = !apps.has(DYNAWAY);
      if (nowSelected) apps.add(DYNAWAY);
      else apps.delete(DYNAWAY);

      const systemes = new Set(prev.access.systemes);
      if (nowSelected) systemes.add(ACCES_D365);

      return {
        ...prev,
        applications: { ...prev.applications, applications: Array.from(apps) },
        access: { ...prev.access, systemes: Array.from(systemes) },
      };
    });
  };

  const updateD365 = (patch: Partial<typeof d>) => {
    setRequest((prev) => ({ ...prev, d365: { ...prev.d365, ...patch } }));
  };

  const toggleRole = (role: string) => {
    updateD365({ roles: d.roles.includes(role) ? d.roles.filter((r) => r !== role) : [...d.roles, role] });
  };

  // Writes straight into request.employee.employee.gestionnaire (the same single "gestionnaire"
  // snapshot every other step reads/shows) rather than a separate D365-only field —
  // D365AccessApproval has no ManagerName column of its own (approver routing matches on position
  // title, never manager), so this mirrors exactly what the standalone D365AccessRequest app's own
  // "Nom du gestionnaire" override does for its one-employee, D365-only request.
  useEffect(() => {
    if (!managerPicker.picked) return;
    const displayName = managerPicker.picked.displayName;
    setRequest((prev) =>
      prev.employee.employee && prev.employee.employee.gestionnaire !== displayName
        ? { ...prev, employee: { ...prev.employee, employee: { ...prev.employee.employee, gestionnaire: displayName } } }
        : prev,
    );
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [managerPicker.picked]);

  const roleCatalog = prefill?.roleCatalog ?? [];
  const groupedRoles = ROLE_GROUPS.map((g) => ({ ...g, roles: roleCatalog.filter((r) => r.startsWith(g.prefix)) })).filter(
    (g) => g.roles.length > 0,
  );
  const ungroupedRoles = roleCatalog.filter((r) => !ROLE_GROUPS.some((g) => r.startsWith(g.prefix)));

  return (
    <div className="step-panel">
      <div className="step-panel__header">
        <span className="step-panel__icon">
          <GridIcon style={{ width: 22, height: 22 }} />
        </span>
        <div>
          <div className="step-panel__title">D365 et Dynaway</div>
          <div className="step-panel__subtitle">
            Indiquez si l'employé a besoin d'un accès Dynamics 365 (D365) ou de Dynaway (gestion des actifs).
          </div>
        </div>
      </div>

      <div className="choice-list">
        <ChoiceCard
          title="Accès D365"
          description="Accès à Dynamics 365 (Comptes fournisseurs, Grand livre, Comptes clients, Approvisionnement)."
          selected={d365Selected}
          onToggle={toggleD365}
          disabled={dynawaySelected}
          disabledHint={dynawaySelected ? 'Requis automatiquement — Dynaway est sélectionné ci-dessous.' : undefined}
        />
        <ChoiceCard
          title="Dynaway"
          description="Gestion des actifs et de la maintenance (EAM) — requiert automatiquement un accès D365."
          selected={dynawaySelected}
          onToggle={toggleDynaway}
        />
      </div>

      {!d365Selected && (
        <div className="step-panel__subtitle" style={{ marginTop: 16 }}>
          Aucun accès D365 requis pour cette demande.
        </div>
      )}

      {d365Selected && !employee && (
        <div className="big-notice">Sélectionnez d'abord un employé à l'étape 1.</div>
      )}

      {d365Selected && employee && isLoadingPrefill && (
        <div className="step-panel__subtitle" style={{ marginTop: 16 }}>Chargement des informations D365…</div>
      )}

      {prefillError && <div className="big-notice">{prefillError}</div>}

      {d365Selected && employee && prefill && (
        <>
          <div className="important-notice" style={{ marginTop: 16 }}>
            <AlertTriangleIcon className="important-notice__icon" />
            <div>
              <strong>Important</strong> — Sélectionnez uniquement les rôles D365 requis pour permettre à l'employé
              d'effectuer son travail.
            </div>
          </div>

          <div className="field-grid field-grid--2">
            <Field label="Access Type" required>
              <select value={d.accessType} onChange={(ev) => updateD365({ accessType: ev.target.value })}>
                <option value="" disabled>Sélectionner…</option>
                {prefill.accessTypeCatalog.map((t) => (
                  <option key={t} value={t}>{ACCESS_TYPE_LABELS[t] ?? t}</option>
                ))}
              </select>
            </Field>
            <Field label="Employé assujetti à une levée (Levy Employee)">
              <select value={d.levyEmployee ? 'Oui' : 'Non'} onChange={(ev) => updateD365({ levyEmployee: ev.target.value === 'Oui' })}>
                <option value="Non">Non</option>
                <option value="Oui">Oui</option>
              </select>
            </Field>
            <Field label="Titre du poste (anglais)" required>
              <input type="text" value={d.jobTitleEnglish} disabled />
            </Field>
            <Field label="Entité légale"><input type="text" value={prefill.legalEntity} disabled /></Field>
            <Field label="Numéro de département">
              <input
                type="text"
                list="d365-cost-centers-list"
                value={d.departmentNumber}
                onChange={(ev) => updateD365({ departmentNumber: ev.target.value })}
                placeholder="Rechercher un centre de coûts…"
              />
              <datalist id="d365-cost-centers-list">
                {costCenters.map((c) => (
                  <option key={c} value={c} />
                ))}
              </datalist>
              <div className="field-hint">Champ de recherche — tapez pour filtrer la liste des centres de coûts.</div>
            </Field>
            <div>
              <PickerField picker={managerPicker} label="Nom du gestionnaire" />
              <div className="field-hint">Champ de recherche — tapez un nom ou un code d'utilisateur pour trouver un compte AD.</div>
            </div>
            <Field label="Limite d'approbation ($)">
              <select value={d.approvalLimit} onChange={(ev) => updateD365({ approvalLimit: ev.target.value })}>
                {approvalLimitOptions.map((v) => (
                  <option key={v} value={v}>{formatLimit(v)}</option>
                ))}
              </select>
            </Field>
          </div>

          <SectionTitle icon={<GridIcon style={{ width: 16, height: 16 }} />}>Rôles D365 requis</SectionTitle>
          <div className="field-grid field-grid--2" style={{ alignItems: 'start' }}>
            {groupedRoles.map((group) => (
              <div key={group.title}>
                <div style={{ fontWeight: 600, fontSize: 13, marginBottom: 6 }}>{group.title}</div>
                <div className="choice-list">
                  {group.roles.map((role) => (
                    <label key={role} style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '4px 0' }}>
                      <input type="checkbox" checked={d.roles.includes(role)} onChange={() => toggleRole(role)} />
                      {role.slice(group.prefix.length).replace(/^\s*-\s*/, '')}
                    </label>
                  ))}
                </div>
              </div>
            ))}
            {ungroupedRoles.length > 0 && (
              <div>
                <div style={{ fontWeight: 600, fontSize: 13, marginBottom: 6 }}>Autres rôles</div>
                <div className="choice-list">
                  {ungroupedRoles.map((role) => (
                    <label key={role} style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '4px 0' }}>
                      <input type="checkbox" checked={d.roles.includes(role)} onChange={() => toggleRole(role)} />
                      {role}
                    </label>
                  ))}
                </div>
              </div>
            )}
          </div>

          <SectionTitle icon={<GridIcon style={{ width: 16, height: 16 }} />}>Détails additionnels</SectionTitle>
          <div className="field-grid field-grid--2">
            <Field label="Détails d'accès aux comptes payables (optionnel)">
              <textarea
                value={d.apAccessDetails}
                onChange={(ev) => updateD365({ apAccessDetails: ev.target.value })}
                placeholder="Précisez toute demande particulière pour l'utilisateur, ainsi qu'un compte utilisateur actuel à reproduire, le cas échéant"
              />
            </Field>
            <Field label="Entités légales additionnelles (optionnel)">
              <textarea value={d.additionalLegalEntities} onChange={(ev) => updateD365({ additionalLegalEntities: ev.target.value })} />
            </Field>
            <Field label="Adresse d'expédition par défaut (optionnel)">
              <input type="text" value={d.defaultShippingAddress} onChange={(ev) => updateD365({ defaultShippingAddress: ev.target.value })} />
            </Field>
            <Field label="Détails additionnels ou commentaires (optionnel)">
              {dynawaySelected && (
                <div className="locked-comment-tag">
                  <LockIcon style={{ width: 13, height: 13, flexShrink: 0 }} />
                  {DYNAWAY_COMMENT_TAG}
                </div>
              )}
              <textarea value={d.comments} onChange={(ev) => updateD365({ comments: ev.target.value })} />
            </Field>
          </div>
        </>
      )}

      <StepFooter />
    </div>
  );
}
