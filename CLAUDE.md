# ScreenTimeManagement

Project root: `C:\dev\Test`. Remote: `github.com/Some-Random-Person/ScreenTimeManagement`.

**Hard rule:** all code for this project goes in this folder. Anything outside it is read-only reference.

## Product

- **What it is:** a parental screen-time manager that enforces **one shared daily budget across all of a child's devices**. Switching device doesn't get around the limits.
- **Users:**
  - Parents manage children, devices and rules from a web dashboard.
  - Children's devices run an agent that tracks and enforces.
- **Platforms:**
  - Windows (Phase 1), then Android, iOS, macOS and Linux.
  - Every platform uses the same server API.
- **Rules per child:**
  - **Daily total**, e.g. 2 h. Counts all non-exempt use across every device.
  - **Per-app limit**, e.g. YouTube 30 min. Counts toward that app's limit *and* the daily total. The app is blocked when either runs out.
  - **Exempt apps**, e.g. Teams. Never blocked and never counted toward the total, but still recorded for reporting.
  - When the total runs out, everything is blocked except exempt apps and a system allowlist (Settings, the dashboard, phone/emergency on mobile).
- **Counting:**
  - Usage is **added up across devices**. Simultaneous use is not merged: 10 min on the phone + 10 min on the PC = 20 min.
  - **Websites count** toward the matching app. youtube.com in a browser = YouTube, and subdomains match.
  - Idle time doesn't count: no input for 2 min **and** no audio playing.
- **Logical apps:**
  - Rules target platform-neutral logical apps.
  - Each one maps to platform identifiers: process name, Android package, Apple bundle, iOS token, or web domain.
  - Unknown identifiers are stored so the parent can assign them to an app.
- **Day boundary:** the child's time zone plus a configurable reset hour.
- **Offline allowance** (internally "lease"):
  - Each device holds a chunk of the remaining time that is set aside for it. The server subtracts it from what the other devices may use, so going offline can never exceed the daily limit.
  - An offline device counts its chunk down and then locks everything that isn't exempt. When it reconnects, it uploads its offline usage, and the unused time goes back into the pool.
  - Reservations expire at the daily reset.
  - Parents can configure it per child, with an optional per-device override: 0–60 min, **default 5**. 0 = lock as soon as the device goes offline.
  - **It must be explained in plain language** wherever a parent sees it (dashboard + README), including a worked example and the trade-off.
- **Tamper resistance (Windows):**
  - The child uses a standard account. The agent runs as a SYSTEM service and watches the user-session helper.
  - The browser extension is force-installed by policy, and InPrivate/Incognito is disabled.
  - Browsers without the extension are closed. Uninstalling needs a parent PIN.
- **Plan:** `C:\Users\thomas\.claude\plans\1-parental-control-is-humble-starlight.md` (Phase 1 scope, layout, build order, verification).
- **Non-goals:** none stated yet.

## How we work

- The user gives instructions and Claude implements them.
- If unsure what the user wants or how they want it, ask. Don't assume.
- Follow KISS and DRY.
  - Smallest change that works. No speculative generality.
  - No single-implementation interfaces. The exception is when the platform requires one, e.g. COM interop.
  - Match the existing style.
- No new dependencies (NuGet, npm or other packages) beyond the approved list below without the user's clear go-ahead. Ask first.
- When designing, rank concerns by severity, say what an addition costs, and flag unknowns instead of guessing.

## Git

- **Branches:**
  - Work on feature branches, never directly on `main`.
  - Name them `feat/<short-name>`, `docs/<short-name>` or `fix/<issue-number>-<short-name>` (e.g. `fix/7-empty-search`). Ask before using any other prefix.
  - When a branch comes from an issue, put the issue number after the prefix, e.g. `feat/12-search` or `docs/15-api-readme`.
  - Local cleanup: before starting new work, update `main`, run `git fetch --prune`, and delete local branches fully merged into `main` with `git branch -d` (never `-D`). Leave remote branches alone.
- **Commits:**
  - Commit when a piece of work is done.
  - Use Conventional Commits (`feat: …`, `fix: …`, `docs: …`, `refactor: …`, `chore: …`).
  - End every commit message with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- **Pushing, PRs and merging:**
  - When work is done, Claude pushes the feature branch and opens a PR against `main` with `gh pr create`.
  - **Claude merges its own PR** once it has reviewed the PR and verified it (builds and tests pass), and merges through the PR.
  - Never push to `main`. Never force-push.
  - Fixes from review go in as new commits. Don't rewrite history on a pushed branch.
  - The PR description says what changed and why, and flags anything uncertain. Whenever Claude adds commits or otherwise changes its own PR, it updates the description so it reflects what the PR actually contains. End it with `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.

## Issues

- Bugs and feature ideas are tracked as GitHub issues, read and managed with `gh`.
- **Picking up work:**
  - Work on an issue when the user points to it ("do #7").
  - Also pick one yourself when the user says something like "take the next one".
  - When picking, bugs come first, then features. Within each group, take the highest impact first: what affects the most people, and anything that stops a main feature from working.
  - When several issues have the same impact, take the oldest first.
- **Linking:** a PR that resolves an issue includes `Closes #<n>` in its description, so the merge closes the issue.
- **New issues:** when Claude spots a bug or loose end outside the current task, it opens an issue for it so it gets tracked. Don't fix it in the current branch.
- **Questions:** the user asks clarifying questions on issues. Claude does not comment on issues. Ask in chat instead.
- **Labels:** if Claude thinks a different label fits an issue better, it asks the user to confirm before changing it.

## Scale and ops

- **Hosting:** self-hosted. The server, Postgres and Caddy (TLS) run through `deploy/docker-compose.yml`, as in the plan.
- **Dev machine:**
  - Windows 11 with .NET SDK 10 and Node.
  - No Docker or Postgres locally, so dev and tests use SQLite. The compose files are written but can't be run here.
- **Target load:** undecided. The working assumption is families, i.e. a few hundred to a few thousand devices per instance. Ask before designing for more.
- **Out of scope:** multi-tenancy beyond families, i18n beyond keeping UI strings in `web/src/i18n/en.json`, and plugin architecture, unless stated otherwise.

## Backend (.NET): confirmed

- **Framework:** .NET 10 (`net10.0`) with a `.slnx` solution file (`ScreenTime.slnx`).
- **Code style:**
  - Nullable and ImplicitUsings on. `GenerateDocumentationFile` on, with XML `<summary>` comments on public API endpoints.
  - File-scoped namespaces and one type per file. Namespaces match project names (`ScreenTime.Core.Models`).
  - Async methods end in `Async`. Private fields use `_camelCase`.
  - DTOs are `sealed record`. Mapping is written by hand in static methods, with no AutoMapper.
  - Comments are sparse.
  - Entities:
    - Mutable classes.
    - `required` for mandatory values, `= null!` for navigation properties, `List<T> X { get; } = []` for collections.
    - Enums are suffixed `Enum`, start at 1, carry `[Description]`, and live in the entity's file.
  - Relationships are configured with the fluent API in `OnModelCreating`.
- **Keys:** Guid primary keys for server entities, since devices generate event IDs offline.
- **Server:** ASP.NET Core minimal API, plus a SignalR `BudgetHub` that pushes `BudgetSnapshot`s to every device of a child.
- **Database:**
  - EF Core code-first with migrations.
  - SQLite in development and tests, Postgres in production.
  - Migrations are generated for Postgres only (`PostgresDesignTimeFactory`) and applied at startup. The SQLite development and test database is created from the model with `EnsureCreated`, so delete `screentime.dev.db` after model changes.
  - Add a migration: `dotnet ef migrations add <Name> --project src/ScreenTime.Server --output-dir Data/Migrations`. `dotnet-ef` is a local tool (`dotnet tool restore`).
  - All `DateTime`s are UTC (`...Utc` names). SQLite can't compare `DateTimeOffset` in queries, so entities use `DateTime`.
- **DI:**
  - Built-in DI. The DbContext is registered with `AddDbContext`, which makes it scoped.
  - Anything that uses the DbContext is `AddScoped`, never a singleton. Background jobs create their own scope.
- **Config:**
  - Production: environment variables only (`ConnectionStrings__DefaultConnection`, `Database__Provider` = `Sqlite` | `Postgres`). Fail fast at startup if either is missing.
  - A gitignored `.env` holds production values, and `.env.example` is committed.
  - Development: `appsettings.Development.json` points at a local SQLite file, so `dotnet run` works without a `.env`.
- **API docs:** built-in `Microsoft.AspNetCore.OpenApi`, document only, at `/openapi/v1.json` in development. No Swagger UI.
- **Error handling:** built-in ProblemDetails (`AddProblemDetails()`, `TypedResults.Problem` / `ValidationProblem`). No result-wrapper type. Malformed requests are 400, not 500.
- **JSON:** enums are serialized as strings, both over HTTP and over SignalR.
- **Auth:**
  - Parents: ASP.NET Core Identity with cookie auth.
  - Devices: pair with a 6-digit code that is exchanged for a long-lived device token. Only the token's hash is stored.
- **Device API:**
  - `POST /api/device/usage`: batched events, idempotent by client event GUID.
  - `POST /api/device/lease`
  - `GET /api/device/state`
- **Projects:**
  - `src/ScreenTime.Core`: platform-neutral domain (`BudgetCalculator`, `AppResolver`, `TimeWindow`, models). Shared by the server and agents, with no I/O.
  - `src/ScreenTime.Server`
  - `src/ScreenTime.Agent.Service`: Windows Service (LocalSystem): sync, SQLite outbox, enforcement, browser policies.
  - `src/ScreenTime.Agent.Helper`: WinForms app in the user session: foreground tracking, idle/audio detection, overlay, tray, native messaging host.
  - `tests/ScreenTime.Core.Tests`, `tests/ScreenTime.Server.Tests` (xUnit).
  - `tools/FakeDevice`: simulates a second device.
- **Packages (approved with the Phase 1 plan):**
  - xUnit (from the template) and `Microsoft.AspNetCore.Mvc.Testing`
  - EF Core: `Microsoft.EntityFrameworkCore.Sqlite`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore.Design` + `dotnet-ef`
  - `Microsoft.AspNetCore.Identity.EntityFrameworkCore`
  - `Microsoft.AspNetCore.SignalR.Client` (agents, FakeDevice, server tests)
  - `Microsoft.Extensions.Hosting.WindowsServices`
  - `Microsoft.AspNetCore.OpenApi` (approved separately)
  - Anything else: ask first.

## Clients: confirmed

- **Browser extension** (`extension/`):
  - Manifest V3, TypeScript, built with Vite. Chrome/Edge first.
  - Reports the active domain, focus and audible state to the Helper via native messaging.
  - Blocks with `declarativeNetRequest` dynamic redirect rules.
  - **Fails closed** if native messaging is unavailable.
- **Parent dashboard** (`web/`):
  - React + Vite + TypeScript, served by the server from `wwwroot`.
  - Pages: login, children, devices/pairing, app catalog, rules editor (including the offline allowance explanation), live today view, grant extra time.

## Unconfirmed defaults

The user has not confirmed anything in this section. Most of it is carried over from the user's conventions in another project. Ask before relying on any item the first time it comes up, and move it to a confirmed section once the user agrees. Licence and CI are also undecided.

### Frontend

- **Style:** Prettier-ish formatting (double quotes, trailing commas, 2-space indent). ESLint 9 flat config.
- **Tests:** light. Don't add frontend test setups unasked.
