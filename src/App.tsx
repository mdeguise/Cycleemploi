import './App.css';
import { QueryClient, QueryClientProvider, useQuery } from '@tanstack/react-query';
import { WizardProvider, useWizard } from './context/WizardContext';
import { Header } from './components/Header';
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
    <WizardProvider demandePar={me.displayName}>
      <WizardBody />
    </WizardProvider>
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
