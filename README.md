# Gestion du cycle d'emploi - Tremblant

Repo: **https://github.com/mdeguise/PowerApp**

A full-stack internal tool for Tremblant's employee lifecycle requests: **Nouvelle intégration**, **Réactivation**, and **Avis de terminaison ou mise à pied temporaire** (offboarding), each as its own multi-step wizard sharing the same app shell.

**Live**: http://cycleemploi (frontend), http://vm-trm-live:8091 (API) — internal/VPN-only, Windows Integrated Auth.

Want to work on this? See **[CONTRIBUTING.md](CONTRIBUTING.md)** for dev environment setup, running locally, and how deploys work.

## Architecture

- **Frontend**: React + TypeScript, built with Vite (`src/`). Static SPA hosted as its own IIS site.
- **Backend**: ASP.NET Core Web API, `net10.0-windows` (`backend/Api/`). Hosted as a **separate** IIS site — not nested under the frontend's site, after Windows Auth on a nested Application produced an unresolved 404 specific to that topology (see git history / `Program.cs`'s auth and CORS comments for the full story). The frontend calls it cross-origin via CORS + `credentials: 'include'`.
- **Auth**: Windows Integrated Authentication (NTLM) — no Entra ID, no app registration, no login screen. The app trusts whatever AD identity the browser already carries. Locally (`dotnet run`, Kestrel) this is `AddNegotiate()`; under IIS it's `IISDefaults.AuthenticationScheme`, since IIS's own Windows Auth module does the handshake there — see the conditional in `Program.cs`.
- **Data**: SQL Server on `vm-trm-sql1`, two databases:
  - `EmployeeLifecycle` — this app's own data (requests, wizard answers, attachments metadata). Full schema via EF Core migrations in `backend/Api/Migrations/`.
  - `Redingote` — externally managed, hourly Workday sync. Read **only** `dbo.WorkdayDemographic` from it (via `WorkdayContext`), never written to.
- **HR confidentiality**: the offboarding "commentaires RH" field lives in its own table (`OffboardingConfidentialComments`), separate from the rest of the request, specifically so it can never get pulled in by a general "get request" query. Read access is gated by AD group membership (`TRM-RH-ADM`), checked via `System.DirectoryServices.AccountManagement`.

### Project structure

```
backend/Api/
  Controllers/       — Auth, Requests, Employees, Catalogs
  Data/               — AppDbContext (EmployeeLifecycle), WorkdayContext (Redingote, read-only)
  Migrations/         — EF Core migrations for EmployeeLifecycle
  Models/Entities/    — EF Core entities
  Models/Dtos/        — API request/response shapes
  Services/           — AD lookups, request numbering, RH-comment authorization
  Program.cs          — auth scheme selection, CORS policy, middleware pipeline
  web.config           — checked in deliberately; the ASP.NET Core SDK transforms this file during
                          `dotnet publish` rather than generating one from scratch, so our Windows
                          Auth config survives every redeploy automatically

src/
  types.ts            — full data model (OnboardingRequest), both onboarding/reactivation and offboarding flows
  context/WizardContext.tsx — wizard state, navigation, validation; step list/count is dynamic based on typeDemande
  api/                — thin fetch client + endpoint definitions, mirrors the backend DTOs
  components/         — shared UI (Header, StepNav, SummarySidebar, form primitives)
  steps/               — one component per wizard step, for both flows
public/web.config      — SPA fallback rewrite rule + Windows Auth config for the frontend's IIS site
```

## Design reference

Tremblant red (`#9c1c2e`) accents, card-based multi-step layout, right-hand summary sidebar with progress tracking, real Tremblant logo and staff photo in `src/assets/`.

## History (abandoned approaches, kept for context)

This went through three earlier plans before landing on the current architecture:

1. **Power Apps Canvas App** (SharePoint-backed) — chosen originally because it needed no special Teams/admin unblock and ran under Microsoft 365 F3 licensing. Abandoned after a day fighting Canvas Studio's authoring/coauthoring reliability (account mismatches, silent property-sync failures, a step-nav bug that never got fixed), even with Microsoft's official Canvas Apps Authoring MCP server driving it. `POWERFX-REFERENCE.md` has the Power Fx translation of the wizard logic from that attempt, kept for historical reference only — it doesn't describe how the app actually works today.
2. **Entra ID SSO + Docker (Linux containers)** — the first full-stack build. Abandoned because it needed two Entra app registrations, a client secret, and Graph API permissions, which triggered enough security-review friction to be worth simplifying away.
3. **Windows Integrated Auth + IIS** (current) — no app registration, no client secret, no login screen at all. The app trusts the same AD identity the user's Windows session already carries.
