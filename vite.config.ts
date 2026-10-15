import { execSync } from 'node:child_process'
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Short commit + build date, baked into the bundle so a "Signaler un problème" report says exactly
// which build the user was on.
function currentVersion(): string {
  const date = new Date().toISOString().slice(0, 10)
  try {
    const sha = execSync('git rev-parse --short HEAD', { stdio: ['ignore', 'pipe', 'ignore'] }).toString().trim()
    return `${sha} · ${date}`
  } catch {
    return `local · ${date}`
  }
}

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  define: { __VERSION__: JSON.stringify(currentVersion()) },
})
