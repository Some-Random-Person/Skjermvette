# Browser extension (`extension/`)

Loaded when working in `extension/`. General rules are in the root `CLAUDE.md`; what the agent must report and enforce is in `docs/plan.md` → "Rules the agents must follow".

## Stack
- Manifest V3, TypeScript compiled by plain `tsc` to ES modules (no bundler). Chrome and Edge (120+); Firefox later.
- `src/` is compiled into `dist/`, and `static/` (the manifest) is copied there by `npm run build`. Load `dist/` unpacked.
- **npm packages (approved, dev only):** `typescript`, `@types/chrome`, `eslint` 9, `@eslint/js`, `typescript-eslint`, `prettier`. Anything else: ask first.
- Checks: **`npm run check`** (types, lint, formatting), the same Prettier settings as `web/`. No tests yet; keep logic that needs no browser in `report.ts` and `blocking.ts`.

## How it reaches the agent
- **Native messaging** host `no.skjermvette.agent`: the Helper's own program (`BrowserHost` in the Helper), which the browser starts and which passes each message on to the Helper running in that session (`BrowserPipe`). See the Helper's `CLAUDE.md`.
- The host only answers the extension id in its manifest's `allowed_origins`. During development the id comes from the `key` in `static/manifest.json` (`dcobicgloeifebalmlahclbcedjnaido`); `register-host.ps1` registers the host for the current user with that id. The installer registers it for every user (`HKLM`), from `installer/no.skjermvette.agent.json`, which has the same id. The stores will give the extension a different id; add it to both.
- The browser keeps the service worker running while the port is open. When it isn't connected, it tries again 2 s after a disconnect and on a 30-second alarm.

## What it reports (`BrowserReport`, the same shape as in the Helper)
- `browser`: `msedge.exe` in Edge (`Edg/` in the user agent), else `chrome.exe`, so the Helper knows which browser program the report is for.
- `active`: the host name of the active tab in the last focused window, for `http`/`https` pages only; null otherwise (new tab, settings, the extension's own pages).
- `audible`: host names of tabs making sound that aren't muted, at most 32.
- Sent when any of these changes (tab switched, navigated, closed, sound started or stopped, window focus), and again after connecting.
- Host names must match `^[a-z0-9.-]{1,253}$` (IPv6 addresses don't); the Helper refuses a report with anything else.

## Blocking (`WebsiteBlocks`, the same shape as in Core)
- The agent's Service works out which websites are blocked (`WebsiteBlocks.For` in Core, by the same rules as closing programs) and sends the list through the Helper and the host when the extension connects and whenever it changes.
- `blocked`: domains whose app is used up, each with the app's name when its own limit ran out (null for the daily total). `allowed`: domains of apps that aren't (exempt apps, apps with time left) and the dashboard. `blockOthers`: the daily total is used up, so any other website is blocked too.
- A domain includes its subdomains and the most specific one wins, as in the agent's `AppResolver`: one `declarativeNetRequest` dynamic rule per domain, with priority = number of labels + 1 (an allow rule wins a tie), plus a catch-all at priority 1 for `blockOthers`. `blockedPathFor` makes the same decision for tabs already open.
- Only pages (`main_frame`) are redirected, to `blocked.html` (`?app=` names the app), not what pages load, so exempt sites keep working.
- When a new list arrives, open tabs on a blocked website, including ones playing in the background, are moved to the blocked page.
- **The browser keeps the dynamic rules**, so the last list stays in force while the agent can't be reached and after a restart (decided by the user on 2026-10-20, instead of blocking everything). A list that isn't valid is ignored.
- The blocked page is in `web_accessible_resources`, so websites can tell the extension is installed.

## Not done yet
- Force-installing it, and blocking developer mode. On Windows outside an Active Directory or Entra ID domain, Chrome and Edge only force-install extensions from the Chrome Web Store and Edge Add-ons (their policy docs), so it has to be published there first (the user's job). Blocking developer mode before then would turn off the unpacked extension itself (decided by the user on 2026-10-29). The installer already turns off private windows.
