# ScreenTimeManagement

Parental screen-time control that works **across all of a child's devices**. One daily budget is shared by the phone, tablet and computer, so switching devices doesn't get around the limits.

> **Status:** early development. The design is settled and the code is being built in phases (see [Roadmap](#roadmap)). Nothing in this repository is ready to install on a child's device yet.

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
- Time is **added up across devices**. 10 min on the phone and 10 min on the PC at the same time count as 20 min.
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

## Planned repository layout

| Path | Contents |
|---|---|
| `src/ScreenTime.Core` | Platform-neutral rules: budget calculation, app matching, day boundaries |
| `src/ScreenTime.Server` | Self-hosted API, SignalR hub and database |
| `src/ScreenTime.Agent.Service` | Windows service: sync, enforcement, browser policies |
| `src/ScreenTime.Agent.Helper` | Windows helper in the child's session: tracking, block overlay, tray |
| `extension/` | Chrome/Edge (Manifest V3) extension |
| `web/` | Parent dashboard (React + Vite) |
| `tests/` | xUnit tests for Core and Server |
| `tools/FakeDevice` | Simulates a second device for testing across devices |
| `deploy/` | `docker-compose.yml` (server, Postgres) for self-hosting, exposed through a Cloudflare Tunnel |

## Roadmap

1. **Phase 1:** core budget engine, server, Windows agent, browser extension, parent dashboard, self-hosting files.
2. **Android:** accessibility service + usage stats, overlay blocking, Device Owner provisioning.
3. **iOS:** Apple Screen Time APIs (FamilyControls / DeviceActivity / ManagedSettings). This starts with a technical spike, because Apple restricts what can be reported off the device.
4. **macOS:** native agent + Safari extension.
5. **Linux:** X11 first; Wayland through a GNOME/KDE extension.

## Development

Requirements: [.NET SDK 10](https://dotnet.microsoft.com/download) and [Node.js](https://nodejs.org/) 22 or newer (for the dashboard). Docker is only needed to try the self-hosting setup.

Build and run the tests:

```bash
dotnet test ScreenTime.slnx
```

Run the server locally. It uses a SQLite file, `screentime.dev.db`, and listens on http://localhost:5131. The OpenAPI document is at `/openapi/v1.json`.

```bash
dotnet run --project src/ScreenTime.Server --launch-profile http
```

After changing the data model, delete `src/ScreenTime.Server/screentime.dev.db` so the development database is recreated. To add a Postgres migration for production:

```bash
dotnet tool restore
```

```bash
dotnet ef migrations add <Name> --project src/ScreenTime.Server --output-dir Data/Migrations
```

### Fake devices

`tools/FakeDevice` pretends to be a phone or PC, so you can try limits across devices before any real agent exists. With the server running, create a pairing code on a child's Devices tab, then pair:

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

Run the tool without arguments for all commands (`state`, `watch`, `list`, `forget`).

### Parent dashboard

The dashboard lives in `web/`. While working on it, run the server (above) and the Vite dev server side by side, then open http://localhost:5173. Vite forwards API calls to the server.

```bash
npm install --prefix web
```

```bash
npm run dev --prefix web
```

Before committing, check types, lint and formatting (`npm run format --prefix web` fixes formatting):

```bash
npm run check --prefix web
```

To have the server itself serve the dashboard at http://localhost:5131, build it into `src/ScreenTime.Server/wwwroot`:

```bash
npm run build --prefix web
```

More commands will be added here as each part is built.

## Contributing

- Work on feature branches: `feat/<name>`, `docs/<name>` or `fix/<issue>-<name>`. Never commit directly to `main`.
- Commit messages follow [Conventional Commits](https://www.conventionalcommits.org/).
- Every change goes through a pull request against `main`.
