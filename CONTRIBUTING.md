# Contributing

## Prerequisites

- **Windows**, domain-joined (or on VPN) — the backend uses Windows-specific libraries for Negotiate auth and AD lookups (`System.DirectoryServices.AccountManagement`), so this doesn't run on macOS/Linux.
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js](https://nodejs.org/) + npm
- Git, and a GitHub account added as a collaborator on [mdeguise/Cycleemploi](https://github.com/mdeguise/Cycleemploi)
- VS Code (or Visual Studio) — no specific extensions required beyond the usual C#/TypeScript ones

## Get the code

```bash
git clone https://github.com/mdeguise/Cycleemploi.git
cd Cycleemploi
```

## SQL Server access (for local dev)

Local dev connects to the *real* `vm-trm-sql1` server (there's no local/mock database) — the connection strings use `Trusted_Connection=True`, so it's your own Windows/AD login that needs the grant below, not a password to configure anywhere.

**Dev and prod are separate databases — since 2026-08-27.** `appsettings.Development.json` points `AppDb` at **`EmployeeLifecycleDev`**; production uses `EmployeeLifecycle` from `appsettings.json`. Before that they were the same database, which meant `dotnet ef database update` run from *any* machine was a production schema change, and there was nowhere to test a migration before it hit real requests. Run migrations against dev first — always. `WorkdayDb` still points at the real `Redingote` in both, because it is read-only and externally managed (hourly Workday sync); there is nothing there for us to break.

`EmployeeLifecycleDev` was created with the same `French_CI_AS` collation as prod on purpose. A collation mismatch between dev and prod produces string-comparison and sorting differences that pass every local test and only surface in production.

Ask whoever has `sysadmin` on `vm-trm-sql1` to run this (swap in your real domain username):

```sql
USE Redingote;
CREATE USER [IDIRECTORY\yourusername] FOR LOGIN [IDIRECTORY\yourusername];
GRANT SELECT ON dbo.WorkdayDemographic TO [IDIRECTORY\yourusername];
GO
USE EmployeeLifecycle;
CREATE USER [IDIRECTORY\yourusername] FOR LOGIN [IDIRECTORY\yourusername];
ALTER ROLE db_datareader ADD MEMBER [IDIRECTORY\yourusername];
ALTER ROLE db_datawriter ADD MEMBER [IDIRECTORY\yourusername];
GO
```

If the login doesn't already exist in AD as a SQL Server login, `CREATE LOGIN [IDIRECTORY\yourusername] FROM WINDOWS;` needs to run first (also needs `sysadmin`).

## Running locally

Backend (Kestrel, port 5211):

```bash
cd backend/Api
dotnet run
```

Frontend (Vite, port 5173):

```bash
npm install
npm run dev
```

Windows Integrated Auth works automatically for both — no login screen, no credentials to enter, as long as you're on the domain/VPN. If `GET /api/auth/me` fails, that almost always means the machine isn't domain-joined/on VPN, not a real auth bug.

### The `.env.local` / `.env.production` gotcha

Vite loads `.env.local` for **every** build mode, including production builds — not just `npm run dev`. `.env.local` (gitignored, machine-specific) points `VITE_API_BASE_URL` at `http://localhost:5211` for local Kestrel testing. `.env.production` (checked in) exists specifically to override that back to the real deployed API origin (`http://vm-trm-live:8091`) for production builds — **don't delete or "clean up" `.env.production` thinking it's redundant with `.env.local`**; it's the fix for a real bug where the local dev URL leaked into a production build and broke the deployed app for every user (see git history around the `.env.production` commit for the full story).

## Deploying

There's no CI/CD pipeline yet — deploys are manual, onto `vm-trm-live` (needs admin rights there):

1. **Frontend**:
   ```bash
   npm run build
   ```
   Copy `dist/*` to `\\vm-trm-live\C$\inetpub\wwwroot\TremblantOnboarding`, removing any stale hashed asset files (old `index-*.js`/`.css`) left over from the previous build.

2. **Backend**:
   ```bash
   cd backend/Api
   dotnet publish -c Release -o <some folder>
   ```
   Stop the `TremblantOnboardingApi` IIS app pool, copy the publish output to `\\vm-trm-live\C$\inetpub\wwwroot\TremblantOnboardingApi`, restart the app pool.

3. **Database schema changes**: if you added an EF Core migration, apply it to **dev first** and only then to prod.

   Dev (safe, this is the default environment locally):
   ```bash
   ASPNETCORE_ENVIRONMENT=Development dotnet ef database update --context AppDbContext
   ```

   Prod — deliberate, and only after it has been proven on dev:
   ```bash
   ASPNETCORE_ENVIRONMENT=Production dotnet ef database update --context AppDbContext
   ```
   Needs a build, so it can't run while the API's IIS app pool has the output locked — stop the pool first. Take a database backup before any migration that drops or alters an existing column; several requests' worth of data lives in there and `Down()` is not a substitute for a backup.

   Scaffolded migrations are a starting point, not a finished artifact. Read the generated `Up()` before running it: EF adds new non-nullable columns with a `defaultValue` and then creates indexes over them, which fails outright on a table that already has rows (and silently produces meaningless data when it doesn't fail). Back-fill explicitly, and prefer a `THROW` over letting a migration quietly produce rows that no longer match anyone.

Sites are separate IIS sites (not nested), each with Windows Auth (NTLM only — Kerberos/Negotiate was dropped because there's no SPN registered for these non-standard ports) and CORS between them. If you touch the auth or CORS setup, read `Program.cs`'s comments first — there's a lot of hard-won context there about why it's structured the way it is.

## Git workflow

Nothing formal yet — commit and push directly, `git pull` before starting work on whichever machine you're on. Worth setting up branch protection / PR review once more than one or two people are regularly pushing.
