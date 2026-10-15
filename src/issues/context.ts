/** Automatic context attached to every "Signaler un problème" report: the last failed API calls
 * (ring buffer), the screen the user is on, the build, and the browser. Frozen when the modal
 * opens, so the report describes what the user saw, not whatever they clicked afterwards. */

interface ApiErrorNote {
  at: string;
  method: string;
  url: string;
  status: number | null;
  message: string;
}

const MAX_ERRORS = 10;
const errors: ApiErrorNote[] = [];
let currentScreen = '';

export function noteError(e: { method: string; url: string; status: number | null; message: string }) {
  errors.push({ at: new Date().toISOString(), method: e.method, url: e.url, status: e.status, message: e.message.slice(0, 300) });
  if (errors.length > MAX_ERRORS) errors.shift();
}

export function setScreen(screen: string) {
  currentScreen = screen;
}

export function getScreen() {
  return currentScreen;
}

function version() {
  const bundle = Array.from(document.scripts)
    .map((s) => s.src.match(/assets\/(index-[\w-]+\.js)/)?.[1])
    .find(Boolean);
  return `${__VERSION__} (${bundle ?? 'dev'})`;
}

export function snapshot() {
  return {
    version: version(),
    url: window.location.href,
    screen: currentScreen,
    browser: navigator.userAgent,
    window: `${window.innerWidth}×${window.innerHeight}`,
    display: `${window.screen.width}×${window.screen.height} @${window.devicePixelRatio}`,
    language: navigator.language,
    reportedAt: new Date().toISOString(),
    recentErrors: [...errors],
  };
}

window.addEventListener('error', (ev) => noteError({ method: 'JS', url: ev.filename || window.location.href, status: null, message: ev.message }));
window.addEventListener('unhandledrejection', (ev) =>
  noteError({ method: 'JS', url: window.location.href, status: null, message: String(ev.reason?.message ?? ev.reason) }),
);
