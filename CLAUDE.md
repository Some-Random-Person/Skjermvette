# ScreenTimeManagement

Project root: `C:\dev\Test`. Remote: `github.com/Some-Random-Person/ScreenTimeManagement`.

**Hard rule:** all code for this project goes in this folder. Anything outside it is read-only reference.

## Where things are written down
- **This file:** rules for every task. Keep it short; area-specific detail goes in the files below.
- [`docs/product.md`](docs/product.md): the full product rules. **Read it before changing how time is counted, limited or blocked.**
- [`docs/plan.md`](docs/plan.md): Phase 1 status, what's left (Windows agent, browser extension, deploy files), the rules agents must follow, and later phases. Update its status table when a step is done.
- `src/ScreenTime.Server/CLAUDE.md` (server, database, auth, device API), `src/ScreenTime.Agent.Service/CLAUDE.md` (Windows agent service), `src/ScreenTime.Agent.Helper/CLAUDE.md` (its user-session Helper) and `web/CLAUDE.md` (dashboard). These load automatically when working in those folders. When a new area starts (e.g. `extension/`, the agent projects), give it its own `CLAUDE.md`.

## Product in short
A parental screen-time manager: **one shared daily budget across all of a child's devices**, with a daily total, per-app limits and exempt apps. Rules that are easy to break by accident:
- **One minute counts as one minute:** use on several apps or devices at the same time counts once (the server merges overlapping time), for the total and for each app's limit.
- **Background audio counts toward the playing app's own limit**, but not twice toward the total. **Websites count** toward the matching app.
- **Offline allowance:** time set aside per device. Going offline can never exceed the daily limit. Default 5 min (0–60). It must always be explained to parents in plain language.
- **A family's own app mapping wins** over the built-in catalog. Mappings are private to the family. **Nothing is shared or sent anywhere automatically** (no telemetry).

## How we work
- The user gives instructions and Claude implements them. If unsure what the user wants or how, ask. Don't assume.
- Follow KISS and DRY: the smallest change that works, no speculative generality, match the existing style. No single-implementation interfaces, except where the platform requires one (e.g. COM interop).
- **No new dependencies** (NuGet, npm or other) beyond the approved lists here and in `web/CLAUDE.md` without the user's clear go-ahead. Ask first.
- When designing, rank concerns by severity, say what an addition costs, and flag unknowns instead of guessing.
- **Running tests:** keep the failure details in the output (`[FAIL]` lines, error messages, stack traces). Never filter down to the summary only, and count a run as failed unless it clearly says "Passed" (#22, #34).
- Build and test everything before a PR: `dotnet test ScreenTime.slnx`, plus `npm run check --prefix web` when the dashboard changed. Click through UI changes in the browser against the real server.

## Git
- **Branches:** never work on `main`. Name them `feat/<short-name>`, `docs/<short-name>` or `fix/<issue-number>-<short-name>`, with the issue number after the prefix when there is one (e.g. `feat/12-search`). Ask before using any other prefix.
- **Before new work:** update `main`, run `git fetch --prune`, and delete local branches fully merged into `main` with `git branch -d` (never `-D`). Leave remote branches alone.
- **Commits:** commit when a piece of work is done. Use Conventional Commits (`feat:`, `fix:`, `docs:`, `refactor:`, `chore:`). End every message with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- **PRs:** push the feature branch and open a PR against `main` with `gh pr create`.
  - **Claude merges its own PR** once it has reviewed it and verified that builds and tests pass, merging through the PR.
  - Never push to `main`. Never force-push. Fixes from review go in as new commits; don't rewrite history on a pushed branch.
  - The description says what changed and why, and flags anything uncertain. Keep it accurate whenever the PR changes. End it with `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.

## Issues
- Tracked as GitHub issues, read and managed with `gh`.
- **Picking up work:** work on an issue when the user points to it ("do #7"), or pick one when asked ("take the next one"): bugs before features, highest impact first (what affects the most people, or stops a main feature from working), oldest first when equal.
- A PR that resolves an issue says `Closes #<n>` in its description.
- **New issues:** when Claude spots a bug or loose end outside the current task, it opens an issue for it rather than fixing it in the current branch.
- Claude doesn't comment on issues; questions go in chat. Ask the user before changing a label.

## Scale and ops
- **Self-hosted:** server and Postgres via `deploy/docker-compose.yml`, exposed through a **Cloudflare Tunnel** (`cloudflared` on the host, not Caddy). Details are in `src/ScreenTime.Server/CLAUDE.md`.
- **Dev machine:** Windows 11, .NET SDK 10, Node. No Docker or Postgres, so dev and tests use SQLite.
- **Target load:** undecided; the working assumption is families (a few hundred to a few thousand devices per instance). Ask before designing for more.
- **Out of scope** unless stated otherwise: multi-tenancy beyond families, i18n beyond keeping UI strings in `web/src/i18n/en.json`, and plugin architecture.

## .NET (all projects)
- .NET 10 (`net10.0`; `net10.0-windows` for the Windows agent and its tests), solution `ScreenTime.slnx`.
- **Projects:**
  - `src/ScreenTime.Core`: platform-neutral domain (`BudgetCalculator`, `AppResolver`, `TimeWindow`, models, `DeviceApi` types), with no I/O.
  - `src/ScreenTime.Server`
  - `src/ScreenTime.Agent.Service`: the Windows agent's service (sync, offline allowance, the Helper's pipe)
  - `src/ScreenTime.Agent.Helper`: the Windows agent's WinForms Helper in the child's session (foreground tracking)
  - `tests/ScreenTime.Core.Tests`, `tests/ScreenTime.Server.Tests`, `tests/ScreenTime.Agent.Tests` (xUnit; Service and Helper; the agent tests use the server tests' `ServerFactory`)
  - `tools/FakeDevice`: a fake device for testing across devices
- **Code style:**
  - Nullable and ImplicitUsings on. File-scoped namespaces, one type per file, namespaces matching project names.
  - Async methods end in `Async`. Private fields use `_camelCase`. Comments are sparse.
  - DTOs are `sealed record`, mapped by hand in static methods (no AutoMapper).
  - Entities are mutable classes: `required` for mandatory values, `= null!` for navigation properties, `List<T> X { get; } = []` for collections.
  - Enums are suffixed `Enum`, start at 1, carry `[Description]`, and live in the entity's file (shared device API enums live in `ScreenTime.Core.DeviceApi`).
  - XML `<summary>` on every API endpoint handler (`GenerateDocumentationFile` is on; missing docs on other public types are not warned about).
- **Approved NuGet packages:**
  - xUnit (template) and `Microsoft.AspNetCore.Mvc.Testing`
  - `Microsoft.EntityFrameworkCore.Sqlite`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore.Design` + `dotnet-ef`
  - `Microsoft.AspNetCore.Identity.EntityFrameworkCore`
  - `Microsoft.AspNetCore.SignalR.Client` (agents, FakeDevice, server tests)
  - `Microsoft.Extensions.Hosting.WindowsServices`
  - `Microsoft.AspNetCore.OpenApi` and `Swashbuckle.AspNetCore.SwaggerUI`
  - `MailKit` (server email, #27), with `System.Security.Cryptography.Pkcs` pinned to a patched version
  - `QRCoder` (server, the authenticator app QR code, #33)
  - `System.Security.Cryptography.ProtectedData` (Windows agent, DPAPI for the device token)
  - Anything else: ask first.

## Unconfirmed defaults
Nothing is pending. When Claude has to assume a convention the user hasn't confirmed, it goes here until the user agrees. Licence and CI are still undecided.
