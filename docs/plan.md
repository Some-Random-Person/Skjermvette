# Plan: Phase 1

The plan for Phase 1: a self-hosted server, the shared budget engine, the parent dashboard, and the Windows agent with its browser extension. It started as the original planning document (2026-10-12) and is kept up to date with decisions made since. `CLAUDE.md` has the rules and conventions; this file has the scope, what's done and what's left.

## Goal
Parents set one daily screen-time budget per child that applies across **all** the child's devices:
- an overall daily limit;
- per-app limits;
- exempt apps.

For example: 2 h total, YouTube at most 30 min, Teams exempt. Usage on every device counts, but **one minute counts as one minute**: time used on several apps or devices at the same time counts once (10 min on a phone and a PC at the same time = 10 min). Websites count toward the matching app, so youtube.com in a browser counts as YouTube. The server is self-hosted.

Android, iOS, macOS and Linux agents are later phases that use the same device API.

## Architecture
```
Parent (any browser) ──> Dashboard (web/, served by the server) ──HTTPS──> Skjermvette.Server (ASP.NET Core + SignalR)
                                                                              │  EF Core → Postgres (prod) / SQLite (dev + tests)
Child devices ──HTTPS + SignalR (device token)─────────────────────────────────┘
  Windows: Service (SYSTEM) + user-session Helper + browser extension (native messaging)
  later:   Android / iOS / macOS / Linux agents using the same device API
```
In production the server runs in Docker with Postgres, behind a **Cloudflare Tunnel** (`cloudflared` on the host; TLS ends at Cloudflare). See `CLAUDE.md` → Scale and ops.

## Status

| # | Step | Status |
|---|---|---|
| 1 | Core budget engine (`BudgetCalculator`, `AppResolver`, `TimeWindow`) with tests | Done (#3, overlap rule #19) |
| 2 | Server: EF Core, parent auth, pairing, device API, SignalR, offline allowance | Done (#5, then #20, #23, #26, #32, #35) |
| 3 | FakeDevice tool | Done (#14, #21) |
| 4 | **Windows agent**: Service + Helper | **In progress.** Done: the Service's sync loop (pairing, token, outbox, offline allowance, live updates, unpairing); the Helper's foreground, audio and idle tracking, sent to the Service over a named pipe; enforcement: warnings at 5 and 1 min, closing programs in use whose time is up, and the full-screen notice; the Service starting the Helper in the signed-in user's session and restarting it (tried installed as SYSTEM). Next: installing. Browser policies wait for the extension (step 5) |
| 5 | **Browser extension** + native messaging host registration | To do |
| 6 | Parent dashboard | Done, moved ahead of 4–5 (#9, then #12, #13, #24, #25, #26) |
| 7 | **Deploy files** (`deploy/docker-compose.yml`, self-hosting README) | To do, together with #6 (forwarded headers behind the tunnel). The README must say that database backups keep data from deleted accounts (#31). The server image must use ASP.NET Core runtime 10.0.13 or later (security fixes, #39) |

**Not built yet** (from the original plan): per-weekday rules in the rules editor.

**Follow-up features**, tracked as issues (#31, delete account, #27, account emails, #33, two-factor sign-in, and its follow-ups #45 and #46, are done):
- #28: email notifications when a child runs out of time
- #29: invite other parents to a family
- #30: public homepage at `/`, dashboard moved to `/dashboard`

## Repository layout

**Done:**
- `Skjermvette.slnx`
- `src/Skjermvette.Core/`: platform-neutral domain shared by the server and agents, with no I/O.
  - `Models/`: `Policy`, `LogicalApp`, `AppMatcher` (process | package | bundle | domain | iosToken), `UsageEvent`, `BudgetSnapshot`, `AppBudget`, `Lease`, `TimeWindow`.
  - `BudgetCalculator`:
    - remaining time per limited app, and remaining total (exempt apps excluded);
    - effective remaining for an app = min(app remaining, total remaining);
    - overlapping time is merged (`UsedTime`).
  - `AppResolver`: raw identifier → logical app. Subdomains match, and the most specific domain wins.
  - `DeviceApi/`: request and response types for the device API, shared by the server, FakeDevice and the agents.
- `src/Skjermvette.Server/`: ASP.NET Core minimal API. Parent auth (Identity, cookie), account settings, children and rules, apps and catalog, devices and pairing, the device API, the SignalR `BudgetHub`. It also serves the built dashboard from `wwwroot`.
- `web/`: React + Vite + TypeScript dashboard.
- `src/Skjermvette.Agent.Service/` (part): pairing and `forget` commands, the device token (DPAPI, folder open only to SYSTEM and Administrators), the SQLite outbox, the 15-second check-in, the SignalR connection, the offline allowance and stopping when unpaired. Receives usage samples from the Helper over a named pipe (open only to SYSTEM and the signed-in user) into its `UsageTracker`. Closes programs whose time is up. Keeps the Helper running in the signed-in user's session (`WTSQueryUserToken` + `CreateProcessAsUser`, as SYSTEM only). See its `CLAUDE.md`.
- `src/Skjermvette.Agent.Helper/` (part): foreground tracking (`SetWinEventHook` plus a 1-second tick, reported by process name), audio (WASAPI `IAudioSessionManager2`, processes making sound, by process name), idle after 2 minutes without input (then only sound counts), and the pipe to the Service. See its `CLAUDE.md`.
- `tests/Skjermvette.Core.Tests/`, `tests/Skjermvette.Server.Tests/`, `tests/Skjermvette.Agent.Tests/` (xUnit, `WebApplicationFactory`, SQLite).
- `tools/FakeDevice/`: pairs like a real device and reports made-up usage. Use it to test across devices.

**To do:**
- `src/Skjermvette.Agent.Service/` (the rest): installed as a Windows Service (LocalSystem, auto-restart on failure).
  - Enforcement (closing blocked programs in use and the Helper's notices are done):
    - sets Edge/Chrome policies in the registry: force-install the extension, disable InPrivate/Incognito, block developer mode.
- `src/Skjermvette.Agent.Helper/` (the rest): small WinForms app in the child's session. It's needed because a SYSTEM service can't see the foreground window or show UI.
  - Audible tabs through the extension (which processes play audio is done).
  - **Native messaging host** for the extension. When a browser is in front, time goes to the app for the active tab's domain, not to "Chrome".
  - A tray icon showing the time left (the warnings and the full-screen "time's up" notice are done).
- `extension/`: Manifest V3, TypeScript, Vite; Chrome and Edge first, Firefox later.
  - Reports the active tab's domain, window focus and audible tabs to the Helper.
  - Blocks used-up domains with `declarativeNetRequest` dynamic redirect rules to a local "blocked" page.
  - **Fails closed:** if native messaging isn't working, it blocks everything except an allowlist.
- `deploy/`: `docker-compose.yml` (server + Postgres 17; server port on `127.0.0.1` only; `cloudflared` on the host) and a self-hosting README. Written but not run on the dev machine, since it has no Docker.

## Rules the agents must follow
These are implemented on the server and in Core; an agent only has to report and enforce correctly.

1. **Report everything in use, as separate events:** the foreground app or tab, **plus** any app or tab playing audio. The server merges overlaps, so one minute counts once toward the total, while each app's own limit counts its own time. Background audio, e.g. YouTube in a hidden tab, still uses up YouTube's limit.
2. **Events** have a device-generated GUID (re-sending is safe), and each lasts at most 1 hour.
3. **Sync every ~15 s** with `POST /api/device/usage`, plus right after reconnecting. Each sync also **renews the offline allowance**. The response carries the device's budget and its new allowance.
4. **Live updates:** connect to the SignalR hub `/hubs/budget` with the device token. The current budget is sent **on connect** (#23) and after every change on any device of the child.
5. **Blocking:** an app is blocked when its effective remaining time is ≤ 0. When the total runs out, block everything except exempt apps and a system allowlist (Settings, the dashboard; phone and emergency calls on mobile).
6. **Offline:** use `BudgetCalculator.CalculateOffline(lease, exemptAppIds, usageSinceGrant)`. When the allowance runs out, block everything that isn't exempt until the device reconnects, then upload the offline usage. An allowance of 0 means locking as soon as the device goes offline. A device that has never checked in blocks nothing.
7. **Unpaired:** if the token is rejected (401), the device has been unpaired. Stop and show that it needs pairing again.
8. **Tamper resistance (Windows):**
   - the child uses a standard (non-admin) account;
   - the Service runs as SYSTEM and restarts the Helper;
   - the extension is force-installed and private windows are disabled;
   - browsers without the extension (e.g. portable ones) are closed;
   - uninstalling needs a parent PIN that the server checks.

   A device that goes silent shows as offline in the dashboard.

## Offline allowance (decided, done on the server and dashboard)
- Each device holds a reserved chunk of the child's remaining time. That's never more than what's left, and limited apps get their own chunk, capped by that app's remaining time.
- The server subtracts every device's chunk from what the other devices may use, so being offline can **never** let the child go over the daily limit.
- Parents set it per child (0–60 min, **default 5**), with an optional per-device override (`Device.OfflineAllowanceOverride`). It applies at the device's next sync.
- It's explained in plain language, with a worked example and the trade-off, in the dashboard (Rules tab) and in the README.

## Later phases (not started; the API already supports them)
- **Android (Kotlin):**
  - Tracking through an AccessibilityService (foreground app and browser URL), plus `UsageStatsManager` to reconcile.
  - Blocking with an overlay.
  - Tamper resistance through Device Owner provisioning (QR code) for a dedicated child device.
- **iOS (Swift):**
  - FamilyControls `.child` authorization. Needs a Family Sharing child account and Apple's FamilyControls distribution entitlement.
  - DeviceActivity threshold events report usage at minute-level detail; ManagedSettings shields do the blocking.
  - Changes from other devices reach the phone through push notifications.
  - **Start with a technical spike**, to confirm the monitor extension can reach the server. Apple restricts what can leave the device.
- **macOS:** a Swift agent using NSWorkspace, plus a Safari web extension.
- **Linux:** X11 tracking first; Wayland through a GNOME Shell or KDE extension.

## Verification for the remaining steps
- **Automated:** `dotnet test Skjermvette.slnx` (Core and server) and `npm run check --prefix web`. Agent logic that doesn't need Windows APIs, e.g. turning foreground and audio samples into events, should get its own unit tests.
- **End-to-end on the dev machine:**
  - Run the server (`dotnet run`). In the dashboard, create a child with YouTube at 2 min, a 5 min total, and Teams exempt.
  - Run the Service and Helper in console mode, and load the unpacked extension in Edge.
  - Watch YouTube in Edge until you're redirected to the blocked page and the overlay appears. Check that Teams still works afterwards.
  - Play YouTube in a background tab while another site is in front. YouTube's limit should run down, but the total shouldn't count twice.
  - Use `FakeDevice` to report 1 min of YouTube as a "phone". The PC's remaining YouTube time should drop straight away (tray and dashboard).
  - **Offline test:** set the offline allowance to 2 min and stop the server. The PC should keep working for 2 min, then lock everything that isn't exempt. Restart the server; the offline usage should be uploaded and the unused time returned. Repeat with the allowance at 0.
  - Read the dashboard explanation texts as a non-technical parent would.
- **Installing the Windows Service** (`sc create`, recovery options) needs admin rights. Ask the user before running it.
