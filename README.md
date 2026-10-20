# Skjermvette

Parental screen-time control that works **across all of a child's devices**. One daily budget is shared by the phone, tablet and computer, so switching devices doesn't get around the limits.

> **Status:** early development. The design is settled and the code is being built in phases (see [Roadmap](#roadmap)). The server, dashboard and most of the Windows agent work, but nothing is ready to install on a child's device yet (see [Installation](#installation)).

## What it does

A parent sets rules per child:

| Rule | Example | Meaning |
|---|---|---|
| Daily total | 2 hours | All counted screen time across every device |
| Per-app limit | YouTube: 30 min | YouTube is blocked after 30 min. That time also counts toward the total. |
| Exempt app | Teams | Never blocked and never counts toward the total |

With those rules:

- After 30 min of YouTube, YouTube is blocked everywhere, and 1 h 30 min is left for other apps.
- Using Teams for 30 min leaves the remaining total unchanged.
- 15 min of YouTube on the phone leaves only 15 min of YouTube on the PC.
- **One minute counts as one minute.** Time used on several apps or devices *at the same time* counts once: 10 min on the phone and 10 min on the PC at the same time count as 10 min. Each app's limit works the same way.
- **Background audio counts** toward that app's own limit: YouTube playing in a hidden browser tab still uses YouTube's 30 minutes (but not extra daily time).
- **Websites count too.** youtube.com in a browser counts as YouTube.

### Offline allowance

Devices report usage to the server every few seconds. If a device loses its internet connection, it can't know what the other devices are doing, so it uses its **offline allowance**:

- While online, each device has a few minutes of the child's remaining time **set aside** for it (default **5 minutes**, adjustable by the parent from 0 to 60, per child or per device).
- If the device goes offline, it can keep using that set-aside time. When the set-aside time runs out, it locks everything except exempt apps until it's back online.
- Set-aside time is taken off what the other devices can use, so **going offline can never let the child go over the daily limit.**
- When the device reconnects, it reports what it used, and any unused set-aside time goes back into the shared pool.

**Example:** the limit is 2 hours and the child has a phone and a PC. The phone goes offline with 10 minutes set aside, so the PC now sees 10 minutes less. The phone uses 6 minutes while offline. When it reconnects, the remaining 4 minutes go back into the pool.

**Choosing a value:**

- **Higher:** fewer interruptions on patchy Wi-Fi or mobile data, but that time can't be used on other devices while it's set aside.
- **Lower, or 0:** strictest. With 0, the device locks as soon as it goes offline.

## Architecture

```
Parent (any browser) ──> Web dashboard ──HTTPS──> Server (ASP.NET Core + SignalR)
                                                     │  Postgres (production) / SQLite (development)
Child devices ──HTTPS + SignalR (device token)──────┘
  Windows: background service + user-session helper + browser extension
  later:   Android / iOS / macOS / Linux agents using the same device API
```

- **Server:** stores rules and usage, calculates the remaining budget, and pushes updates to every device the moment anything changes.
- **Agents** (one per platform): track the foreground app or website, report usage, and block apps when their time is up.
- **Browser extension:** tells the agent which site is active and blocks sites whose time is up.
- **Dashboard:** the web app parents use to manage children, devices and rules, and to watch today's usage.

## Repository layout

| Path | Contents | Status |
|---|---|---|
| `src/Skjermvette.Core` | Platform-neutral rules: budget calculation, app matching, day boundaries | Done |
| `src/Skjermvette.Server` | Self-hosted API, SignalR hub and database; also serves the dashboard | Done |
| `web/` | Parent dashboard (React + Vite) | Done |
| `src/Skjermvette.Agent.Service` | Windows service: sync, offline allowance, closing programs, starting the Helper | Mostly done; browser policies to do |
| `src/Skjermvette.Agent.Helper` | Windows helper in the child's session: tracking, warnings and the "time's up" notice | Mostly done; tray icon and browser link to do |
| `tests/` | xUnit tests for Core, the server and the Windows agent | Done |
| `tools/FakeDevice` | Pretends to be a phone or PC, for testing across devices | Done |
| `installer/` | Windows installer (WiX) for the Service and Helper, with pairing | Done |
| `extension/` | Chrome/Edge (Manifest V3) extension | Not started |
| `deploy/` | `docker-compose.yml` (server, Postgres) for self-hosting behind a Cloudflare Tunnel | Written, not yet run on a real server |

[docs/plan.md](docs/plan.md) has the detailed status, and [docs/product.md](docs/product.md) the full product rules.

## Roadmap

1. **Phase 1:** core budget engine, server, Windows agent, browser extension, parent dashboard, self-hosting files.
2. **Android:** accessibility service + usage stats, overlay blocking, Device Owner provisioning.
3. **iOS:** Apple Screen Time APIs (FamilyControls / DeviceActivity / ManagedSettings). This starts with a technical spike, because Apple restricts what can be reported off the device.
4. **macOS:** native agent + Safari extension.
5. **Linux:** X11 first; Wayland through a GNOME/KDE extension.

## Installation

> **Not ready for real use yet.** There's no browser extension yet, so websites aren't counted or blocked on Windows, and the Windows installer isn't signed. To try everything on one computer, follow [Development](#development) instead.

### Server and dashboard

The server is one ASP.NET Core app that also serves the dashboard. It runs in Docker with Postgres 17 and sits behind a [Cloudflare Tunnel](https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/), so it doesn't handle HTTPS itself.

> The Docker files in `deploy/` haven't been run on a real server yet (they were written on a computer without Docker). Expect rough edges, and please open an issue if something doesn't work.

You need:

- a Linux computer with [Docker](https://docs.docker.com/engine/install/) and Docker Compose;
- a domain on Cloudflare, and `cloudflared` installed on the same computer;
- an SMTP account for sending emails (sign-in codes, confirmations, password resets).

**1. Get the code:**

```bash
git clone https://github.com/Some-Random-Person/Skjermvette.git
```

Run the commands below from the `Skjermvette` folder it creates.

**2. Configure.** Copy [`deploy/.env.example`](deploy/.env.example) to `deploy/.env` and fill it in. The server stops at startup with a clear message if something is missing.

| Variable | What it is |
|---|---|
| `POSTGRES_PASSWORD` | The database password. Letters and digits only, e.g. from `openssl rand -hex 32`. |
| `App__PublicUrl` | The address parents open, e.g. `https://skjermvette.example.com`. Every link in an email starts with it. |
| `Email__From` | The sender, e.g. `Skjermvette <skjermvette@example.com>` |
| `Email__Smtp__Host`, `Email__Smtp__Port`, `Email__Smtp__User`, `Email__Smtp__Password` | Your SMTP server. Port 465 uses TLS from the start; any other port (e.g. 587) must support STARTTLS. |

**3. Start** the server and database. The database tables are created and updated automatically. The server listens on `127.0.0.1:8080`, so only programs on the same computer can reach it.

```bash
docker compose -f deploy/docker-compose.yml up -d --build
```

**4. Connect the tunnel.** In Cloudflare, create a tunnel with a public hostname for your address and the service `http://127.0.0.1:8080` (not `localhost`, which can mean the IPv6 address the server doesn't listen on). Turn on **Always Use HTTPS** for the domain: sign-in cookies only work over HTTPS.

**5. Create an account** at your public address: register, open the confirmation link in the email, then sign in. Every sign-in needs a second step: a code by email, or from an authenticator app you can turn on under Settings → Security.

**Updating:** get the new code with `git pull`, then rebuild (`--pull` also fetches .NET security fixes) and restart:

```bash
docker compose -f deploy/docker-compose.yml build --pull
```

```bash
docker compose -f deploy/docker-compose.yml up -d
```

**Backups:** the database is in the `db` volume. To save a copy:

```bash
docker compose -f deploy/docker-compose.yml exec -T db pg_dump -U skjermvette skjermvette > skjermvette-backup.sql
```

Things to know:

- **Backups keep deleted accounts.** Deleting an account removes it from the database, but not from backups taken before (#31). Tell parents how long you keep backups.
- **The `keys` volume** holds the keys that protect sign-in cookies and email links. If it's lost, everyone is signed out and links already sent stop working; nothing else is lost.
- **Visitors' addresses** come from Cloudflare (`CF-Connecting-IP`). The limits on sending emails and on pairing attempts are per address. They're only trusted from the Docker network in `docker-compose.yml` (`172.30.80.0/24`; change it there if it clashes with another network). To check that it works, add `Logging__LogLevel__Microsoft.AspNetCore.HttpOverrides=Debug` to `.env`, run the `up -d` command again, sign in, and look for `Unknown proxy` in `docker compose -f deploy/docker-compose.yml logs server`. There should be none.

#### Without Docker

Build with the .NET SDK 10 and Node.js 22 or newer. Build the dashboard first, so it's included in the server:

```bash
npm ci --prefix web
```

```bash
npm run build --prefix web
```

```bash
dotnet publish src/Skjermvette.Server -c Release -o publish/server
```

Run it with the [ASP.NET Core Runtime 10.0.13 or later](https://dotnet.microsoft.com/download/dotnet/10.0) (earlier 10.0 versions have security holes, #39) and a Postgres database whose user can create tables. Set the variables above (except `POSTGRES_PASSWORD`) in the server's environment, plus:

| Variable | What it is |
|---|---|
| `Database__Provider` | `Postgres` (or `Sqlite` for a quick test) |
| `ConnectionStrings__DefaultConnection` | How to reach the database, e.g. `Host=localhost;Database=skjermvette;Username=skjermvette;Password=…` |
| `App__ProxyNetwork` | Only if `cloudflared` runs on another computer: the network it connects from, e.g. `192.168.1.0/24`. On the same computer it's trusted already. |

Listen on the local machine only, and let the tunnel forward to it:

```bash
ASPNETCORE_URLS=http://127.0.0.1:8080 dotnet publish/server/Skjermvette.Server.dll
```

### Windows agent

The agent is two programs: a **Service** that runs as SYSTEM and talks to the server, and a **Helper** that the Service starts in the signed-in user's session, to see what's in front and show notices. One installer puts in both, pairs the computer with a child and starts the service.

> **The agent closes programs.** When an app's time is up it's closed, and when the daily total is used up everything that isn't exempt is closed. Before installing it on a computer you use yourself, pair it with a test child whose rules mark the programs you need as exempt (e.g. your browser, terminal and Remote Desktop), and make sure the server is running so the agent learns them at its first check-in.

The child should use a standard (non-administrator) account. Nothing else needs installing: the installer includes .NET.

**1. Build the installer** on a computer with the .NET SDK 10 (it doesn't have to be the child's):

```bash
dotnet build installer -c Release
```

It's written to `installer/bin/Release/Skjermvette.Agent.msi`. Add `-p:Version=1.2.3` to number a release; a higher number installs over an older one and keeps the pairing.

**2. Create a pairing code.** In the dashboard, open the child's Devices tab and create a code. It lasts 10 minutes.

**3. Install** on the child's computer, as an administrator. Open the `.msi` and fill in your server's address and the code. The installer isn't signed yet, so Windows SmartScreen warns about it first (**More info → Run anyway**). Or install without questions, from a command prompt opened as administrator:

```powershell
msiexec /i Skjermvette.Agent.msi SERVER=https://skjermvette.example.com PAIRCODE=123456 /qn
```

If the code is wrong or has run out, or the server can't be reached, nothing is installed; make a new code and try again. The device shows as online on the child's Devices tab within about 15 seconds. The programs are in `C:\Program Files\Skjermvette\Agent`, and the device's token is stored encrypted in `%ProgramData%\Skjermvette\Agent`, which only SYSTEM and administrators can open.

**To remove it**, uninstall **Skjermvette Agent** in Settings → Apps (as an administrator). That also forgets the pairing on the computer; unpair the device in the dashboard too.

> Uninstalling only needs an administrator, not yet the parent PIN the plan asks for.

#### Without the installer

For testing a change to the agent without building the installer: publish the Service, and the Helper into a `Helper` folder below it, then pair and register the service as an administrator. This needs the [.NET Desktop Runtime 10](https://dotnet.microsoft.com/download/dotnet/10.0) on the computer.

```bash
dotnet publish src/Skjermvette.Agent.Service -c Release -o "C:\Program Files\Skjermvette\Agent"
```

```bash
dotnet publish src/Skjermvette.Agent.Helper -c Release -o "C:\Program Files\Skjermvette\Agent\Helper"
```

```powershell
& "C:\Program Files\Skjermvette\Agent\Skjermvette.Agent.Service.exe" pair 123456 --server https://skjermvette.example.com
```

```powershell
sc.exe create "Skjermvette Agent" binPath= "C:\Program Files\Skjermvette\Agent\Skjermvette.Agent.Service.exe" start= auto
```

```powershell
sc.exe failure "Skjermvette Agent" reset= 86400 actions= restart/5000/restart/5000/restart/5000
```

```powershell
sc.exe start "Skjermvette Agent"
```

Add `--name <name>` to `pair` to call the device something other than the computer's name. To remove it: `sc.exe stop "Skjermvette Agent"`, `sc.exe delete "Skjermvette Agent"`, then `Skjermvette.Agent.Service.exe forget`.

## Development

Requirements: Windows (for the agent), the [.NET SDK 10](https://dotnet.microsoft.com/download) and [Node.js](https://nodejs.org/) 22 or newer. No database server or Docker is needed: development and tests use SQLite files.

### Build and test

```bash
dotnet test Skjermvette.slnx
```

```bash
npm ci --prefix web
```

```bash
npm run check --prefix web
```

`npm run check` checks the dashboard's types, lint and formatting (`npm run format --prefix web` fixes formatting). Both must pass before a pull request.

### Run the server and dashboard

Start the server in one terminal. It uses a SQLite file, `src/Skjermvette.Server/skjermvette.dev.db`, and listens on http://localhost:5131:

```bash
dotnet run --project src/Skjermvette.Server --launch-profile http
```

Start the dashboard in another, then open **http://localhost:5173**. Vite reloads on every change and forwards API calls to the server:

```bash
npm run dev --prefix web
```

The page the server opens by itself (http://localhost:5131) only shows the dashboard once you've built it into the server with `npm run build --prefix web`. The API can be browsed and tried at http://localhost:5131/docs (Swagger UI; the raw OpenAPI document is at `/openapi/v1.json`).

### First sign-in

In development, **emails aren't sent**: each one is written as a file to `src/Skjermvette.Server/emails/`. Open the newest `.eml` file in a text editor or mail program.

1. Register at http://localhost:5173. Any email address works; the password needs at least 10 characters.
2. Open the confirmation email and follow its link (links point at the Vite dev server, so keep it running).
3. Sign in. The second step's code is in the newest email.
4. Add a child, set its rules, and pair devices from its Devices tab.

### Database

After changing the data model, stop the server and delete `src/Skjermvette.Server/skjermvette.dev.db`. It's created again, empty, at the next start. Production uses Postgres migrations, which are applied at startup. To add one:

```bash
dotnet tool restore
```

```bash
dotnet ef migrations add <Name> --project src/Skjermvette.Server --output-dir Data/Migrations
```

### Fake devices

`tools/FakeDevice` pretends to be a phone or PC, so you can try limits across devices without a real agent. With the server running, create a pairing code on a child's Devices tab, then pair:

```bash
dotnet run --project tools/FakeDevice -- pair 123456 --name Phone --platform Android
```

Report 15 minutes of YouTube on that phone, then see what the child has left:

```bash
dotnet run --project tools/FakeDevice -- use Phone android com.google.android.youtube 15
```

Keep "using" an app the way a real agent reports it, every 15 seconds, and watch updates pushed from other devices (Ctrl+C to stop):

```bash
dotnet run --project tools/FakeDevice -- live PC website www.youtube.com
```

Run the tool without arguments for all commands (`state`, `watch`, `list`, `forget`). Paired fake devices are saved in `%LOCALAPPDATA%\Skjermvette\FakeDevice`.

### Windows agent in a console

The Service and Helper can run in two terminals, without installing anything or administrator rights. In Development the Service keeps its data in `%LOCALAPPDATA%\Skjermvette\Agent`, and it can't start the Helper itself (that needs SYSTEM), so you start both.

> **It really closes programs** on your computer. First give the test child generous limits, and mark everything you need to keep open (your editor, terminal, browser, Remote Desktop) as exempt.

Pair with a code from the child's Devices tab:

```bash
dotnet run --project src/Skjermvette.Agent.Service -- pair 123456 --server http://localhost:5131
```

Run the Service:

```bash
dotnet run --project src/Skjermvette.Agent.Service
```

Run the Helper in a second terminal. Debug builds print what's in use every second:

```bash
dotnet run --project src/Skjermvette.Agent.Helper
```

Stop both with Ctrl+C. To pair again, run `dotnet run --project src/Skjermvette.Agent.Service -- forget` and unpair the old device in the dashboard.

## How AI is used in this project

I build Skjermvette on my own, working with an AI coding agent, [Claude Code](https://claude.com/claude-code) (Anthropic's Claude Opus 5.5). **I decide what gets built and how the product behaves; Claude writes most of the code, tests and documentation**, and opens and merges the pull requests once the checks pass.

**The app itself contains no AI.** Nothing a child does, and nothing a parent enters, is sent to an AI service, or anywhere other than your own server.

### Who does what

| | Me | Claude |
|---|---|---|
| Product rules (how time counts, the offline allowance, privacy) | Decide | Asks when something is unclear, points out trade-offs and risks |
| Code, tests and docs | Give the instructions, review the result | Writes them, following the project's written rules |
| Pull requests | Review before or after merging, depending on the size of the PR | Opens them, reviews its own change, merges once builds and tests pass |
| New dependencies | Approve each one | Asks first; can't add any on its own |
| Running the agent on a real computer | Approve every run | Never starts it without my go-ahead |

### How it's kept in check

- **Written rules.** [`CLAUDE.md`](CLAUDE.md) and one `CLAUDE.md` per area (server, agent, dashboard) hold the conventions, decisions and pitfalls. Claude reads them at the start of every task, and they're updated whenever a decision is made.
- **Every change is a pull request** with a description of what changed, why, and what's uncertain. Nothing is pushed straight to `main`.
- **Tests must pass** before merging: `dotnet test Skjermvette.slnx` and the dashboard's type, lint and format checks. Dashboard changes are also clicked through in a browser against the real server.
- **It's visible in the history.** Every commit Claude wrote ends with `Co-Authored-By: Claude`, so `git log` shows exactly which ones.
- **Security-sensitive parts** (passwords, two-factor sign-in, keeping the child from stopping the agent) are written against published guidance (OWASP, NIST) and covered by tests. Protections that only work as SYSTEM were also tried by hand on a real Windows machine.
- **Mistakes become rules.** When an early test run of the agent closed every program on my computer (2026-10-16), the product rule "a computer that has never reached the server blocks nothing" was added, and test runs now need my own tools exempt first.

### In numbers (2026-10-12 to 2026-10-19)

| | |
|---|---|
| Commits | 65, of which 64 co-written by Claude |
| Merged pull requests | 44 |
| Issues | 21 |
| Test files | 32 |

### What this means for you

- I stand behind and am responsible for every release, no matter who or what wrote the code.
- Found a bug? Open an issue. It's treated the same whether the code came from me or from Claude.
- Outside contributors may use AI tools, as long as they mark it the same way. I won't hold anyone to a higher standard than I hold myself. You're still responsible for the code you submit, no matter who or what wrote it.

## Contributing

- Work on feature branches: `feat/<name>`, `docs/<name>` or `fix/<issue>-<name>`. Never commit directly to `main`.
- Commit messages follow [Conventional Commits](https://www.conventionalcommits.org/).
- Every change goes through a pull request against `main`, with `dotnet test Skjermvette.slnx` (and `npm run check --prefix web` for dashboard changes) passing.
