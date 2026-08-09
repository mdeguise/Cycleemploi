import './App.css';
import type { ReactNode } from 'react';
import { QueryClient, QueryClientProvider, useQuery } from '@tanstack/react-query';
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { Link } from 'react-router-dom';
import { WizardProvider, useWizard } from './context/WizardContext';
import { Header } from './components/Header';
import tremblantLogo from './assets/logo-tremblant.png';
import { StepNav } from './components/StepNav';
import { SummarySidebar } from './components/SummarySidebar';
import { TipBanner } from './components/TipBanner';
import { Step1Employee } from './steps/Step1Employee';
import { Step2Position } from './steps/Step2Position';
import { Step3Access } from './steps/Step3Access';
import { Step4Equipment } from './steps/Step4Equipment';
import { Step5Applications } from './steps/Step5Applications';
import { Step6Review } from './steps/Step6Review';
import { Step2Cessation } from './steps/Step2Cessation';
import { Step3DepartmentComments } from './steps/Step3DepartmentComments';
import { StepReviewOffboarding } from './steps/StepReviewOffboarding';
import { TYPE_DEMANDE_TERMINAISON } from './types';
import { ApiProvider } from './api/ApiContext';
import { useApi } from './api/ApiContext';
import { D365RolesAdminPage } from './admin/D365RolesAdminPage';
import { D365UserRolesCorrectionPage } from './admin/D365UserRolesCorrectionPage';

const ONBOARDING_STEP_COMPONENTS = [
  Step1Employee,
  Step2Position,
  Step3Access,
  Step4Equipment,
  Step5Applications,
  Step6Review,
];

const OFFBOARDING_STEP_COMPONENTS = [Step1Employee, Step2Cessation, Step3DepartmentComments, StepReviewOffboarding];

const queryClient = new QueryClient();

function AdminLayout({ title, children }: { title: string; children: ReactNode }) {
  return (
    <div className="app-shell">
      <header className="app-header">
        <div className="app-header__brand">
          <img src={tremblantLogo} alt="Tremblant" className="app-header__logo" />
          <div className="app-header__title">{title}</div>
        </div>
        <div style={{ display: 'flex', gap: 16 }}>
          <Link to="/admin/d365-roles">Rôles D365 par code d'emploi</Link>
          <Link to="/admin/d365-user-roles">Correction des rôles D365</Link>
          <Link to="/">Retour à l'application</Link>
        </div>
      </header>
      <div className="app-body">{children}</div>
    </div>
  );
}

function WizardBody() {
  const { currentStep, request } = useWizard();
  const components =
    request.typeDemande === TYPE_DEMANDE_TERMINAISON ? OFFBOARDING_STEP_COMPONENTS : ONBOARDING_STEP_COMPONENTS;
  const StepComponent = components[currentStep] ?? components[0];

  return (
    <div className="app-shell">
      <Header />
      <div className="app-body">
        <StepNav />
        <StepComponent />
        <SummarySidebar />
      </div>
      <TipBanner />
    </div>
  );
}

/** Fetches the signed-in user's profile before the wizard mounts — WizardProvider needs a display
 * name up front for the "Demandé par" field, and this doubles as the first real proof the backend
 * connection + Windows Auth actually works end to end. A failure here most likely means the site
 * isn't in the browser's trusted/intranet zone (so it never sent Windows credentials) rather than
 * a real "not signed in" state — there's no login screen to fall back to. */
function AuthenticatedApp() {
  const api = useApi();
  const { data: me, isLoading, isError, error } = useQuery({
    queryKey: ['me'],
    queryFn: () => api.auth.me(),
  });

  if (isLoading) {
    return (
      <div style={{ display: 'flex', height: '100vh', alignItems: 'center', justifyContent: 'center' }}>
        Chargement…
      </div>
    );
  }

  if (isError || !me) {
    return (
      <div style={{ display: 'flex', height: '100vh', alignItems: 'center', justifyContent: 'center' }}>
        Impossible de charger le profil : {error instanceof Error ? error.message : 'erreur inconnue'}
      </div>
    );
  }

  return (
    <BrowserRouter>
      <Routes>
        <Route
          path="/admin/d365-roles"
          element={
            <AdminLayout title="Administration — Rôles de sécurité D365">
              <D365RolesAdminPage />
            </AdminLayout>
          }
        />
        <Route
          path="/admin/d365-user-roles"
          element={
            <AdminLayout title="Administration — Correction des rôles D365">
              <D365UserRolesCorrectionPage />
            </AdminLayout>
          }
        />
        <Route
          path="/*"
          element={
            <WizardProvider demandePar={me.displayName}>
              <WizardBody />
            </WizardProvider>
          }
        />
      </Routes>
    </BrowserRouter>
  );
}

function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <ApiProvider>
        <AuthenticatedApp />
      </ApiProvider>
    </QueryClientProvider>
  );
}

export default App;
