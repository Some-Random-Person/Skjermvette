# Product rules

How Skjermvette behaves, in detail. Read this before changing anything that affects how time is counted, limited or blocked. The short version is at the top of `CLAUDE.md`; the build status is in [plan.md](plan.md).

## What it is
- A parental screen-time manager that enforces **one shared daily budget across all of a child's devices**. Switching device doesn't get around the limits.
- **Users:**
  - Parents manage children, devices and rules from a web dashboard.
  - Children's devices run an agent that tracks and enforces.
- **Platforms:** Windows (Phase 1), then Android, iOS, macOS and Linux. Every platform uses the same server API.
- **Non-goals:** none stated yet.

## Rules per child
- **Daily total**, e.g. 2 h. Counts all non-exempt use across every device.
- **Per-app limit**, e.g. YouTube 30 min. Counts toward that app's limit *and* the daily total. The app is blocked when either runs out.
- **Exempt apps**, e.g. Teams. Never blocked and never counted toward the total, but still recorded for reporting.
- When the total runs out, everything is blocked except exempt apps and a system allowlist (Settings, the dashboard; phone and emergency calls on mobile).
- **Extra time:** a parent can add minutes to today's total only.

## Counting
- **One minute counts as one minute.** Usage from every device counts, but time when several apps or devices are in use at once counts only once: the server merges overlapping time. 10 min on the phone and 10 min on the PC *at the same time* = 10 min. (Changed by the user on 2026-10-14; it was previously added up.)
- The same goes for each app's limit: YouTube on two devices at once counts 1 min of YouTube per minute.
- **Background audio counts toward the playing app's own limit** (e.g. YouTube in a hidden tab), but not twice toward the total. Agents report everything in use: the foreground app plus any app playing audio.
- **Websites count** toward the matching app. youtube.com in a browser = YouTube, and subdomains match (the most specific domain wins).
- Idle time doesn't count: after 2 min without input, only apps playing audio count (not the app in front, unless it's the one playing). No input **and** no audio means nothing counts. (Decided by the user on 2026-10-16.)
- **Day boundary:** the child's time zone plus a configurable reset hour.

## Logical apps
- Rules target platform-neutral logical apps. Each one maps to platform identifiers: process name, Android package, Apple bundle, iOS token, or web domain.
- Unknown identifiers are stored, so the parent can assign them to an app ("Not recognised yet", which can also create a new app in one step).
- **Mappings a family makes are private to that family.** Nothing is shared between families or sent anywhere automatically.
- **Built-in catalog** (`SeedCatalog.cs`): grown by hand with each release, followed by a migration.
  - App ids continue the sequence (`Id(n)`); never reuse or renumber them.
  - Only add identifiers that are known to be right. iOS bundle ids are informational, since iOS reports opaque tokens.
- **A family's own mapping wins** over a built-in identifier with the same value (`ToCoreCatalog`), so catalog updates never change how a family's existing mappings count.
- **Suggestions:** parents can email one of their family's identifiers to the maintainer with "Suggest", a `mailto:` they review and send themselves.
  - The address is `appSuggestionEmail` in `web/src/config.ts`. While it's `null`, the button is hidden.
  - The user is setting up a dedicated address; don't use their personal one.
  - Automatic or telemetry-style collection was decided against: it's children's data, and would need GDPR opt-in and a central service.

## Offline allowance (internally "lease")
- Each device holds a chunk of the remaining time that is set aside for it. The server subtracts it from what the other devices may use, so **going offline can never exceed the daily limit**.
- An offline device counts its chunk down, then locks everything that isn't exempt. When it reconnects, it uploads its offline usage, and the unused time goes back into the pool.
- Reservations expire at the daily reset.
- A device that has never checked in with the server (so has never had an allowance) blocks nothing, so a computer that isn't set up yet can't be locked. (Decided by the user on 2026-10-16.)
- Parents set it per child, with an optional per-device override: 0–60 min, **default 5**. 0 = lock as soon as the device goes offline.
- **It must be explained in plain language** wherever a parent sees it (dashboard and README), with a worked example and the trade-off.

## Devices
- A device pairs with a one-time 6-digit code from the dashboard (valid 10 minutes).
- Unpairing hides the device everywhere and revokes its token. Its usage today still counts.

## Tamper resistance (Windows)
- The child uses a standard account. The agent runs as a SYSTEM service and watches the user-session helper.
- The browser extension is force-installed by policy, and InPrivate/Incognito is disabled.
- Browsers without the extension are closed. Uninstalling needs a parent PIN.
