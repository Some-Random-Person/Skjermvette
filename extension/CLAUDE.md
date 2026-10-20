# Browser extension (`extension/`)

Loaded when working in `extension/`. General rules are in the root `CLAUDE.md`; what the agent must report and enforce is in `docs/plan.md` → "Rules the agents must follow".

## Stack
- Manifest V3, TypeScript compiled by plain `tsc` to ES modules (no bundler). Chrome and Edge (120+); Firefox later.
- `src/` is compiled into `dist/`, and `static/` (the manifest) is copied there by `npm run build`. Load `dist/` unpacked.
- **npm packages (approved, dev only):** `typescript`, `@types/chrome`, `eslint` 9, `@eslint/js`, `typescript-eslint`, `prettier`. Anything else: ask first.
- Checks: **`npm run check`** (types, lint, formatting), the same Prettier settings as `web/`. No tests yet; keep logic that needs no browser in `report.ts`.

## How it reaches the agent
- **Native messaging** host `no.skjermvette.agent`: the Helper's own program (`BrowserHost` in the Helper), which the browser starts and which passes each message on to the Helper running in that session (`BrowserPipe`). See the Helper's `CLAUDE.md`.
- The host only answers the extension id in its manifest's `allowed_origins`. During development the id comes from the `key` in `static/manifest.json` (`dcobicgloeifebalmlahclbcedjnaido`); `register-host.ps1` registers the host for the current user with that id. The stores will give the extension a different id.
- The browser keeps the service worker running while the port is open. When it isn't connected, it tries again 2 s after a disconnect and on a 30-second alarm.

## What it reports (`BrowserReport`, the same shape as in the Helper)
- `browser`: `msedge.exe` in Edge (`Edg/` in the user agent), else `chrome.exe`, so the Helper knows which browser program the report is for.
- `active`: the host name of the active tab in the last focused window, for `http`/`https` pages only; null otherwise (new tab, settings, the extension's own pages).
- `audible`: host names of tabs making sound that aren't muted, at most 32.
- Sent when any of these changes (tab switched, navigated, closed, sound started or stopped, window focus), and again after connecting.
- Host names must match `^[a-z0-9.-]{1,253}$` (IPv6 addresses don't); the Helper refuses a report with anything else.

## Not done yet
- Blocking used-up websites (`declarativeNetRequest` redirects to a local "blocked" page) and failing closed when the agent can't be reached.
- Force-installing it and turning off private windows through browser policies (the Service). Outside a domain or device management, Chrome and Edge probably only force-install extensions from their own stores; not checked yet.
