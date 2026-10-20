# Windows agent: Helper

Loaded when working in `src/Skjermvette.Agent.Helper/`. General rules and the C# code style are in the root `CLAUDE.md`; the Service it reports to is described in `src/Skjermvette.Agent.Service/CLAUDE.md`.

## Shape
- WinForms, `net10.0-windows`, no window of its own (`HelperContext`, an `ApplicationContext`). It runs in the child's session because a SYSTEM service can't see the foreground window or show UI.
- One per session: the Service keeps one, and starts it with `--service` at High integrity, so the child's programs can't close or change it (see the Service's `CLAUDE.md`). Started by hand, it uses a `Local\` mutex instead. Debug builds are console apps that print what's in use; Release builds are `WinExe`. The Service starts it in the signed-in user's session and restarts it when it stops (`HelperWorker` in the Service). In development the Service doesn't run as SYSTEM and can't, so start it by hand (`dotnet run --project src/Skjermvette.Agent.Helper`).
- Win32 calls live in `NativeMethods` (`DllImport`), WASAPI's COM interfaces in `AudioSessions`. Anything that doesn't need Windows goes in `Sampler`, `RecentlyHeard` or `NoticeText`, so it can be unit tested.

## What it reports
- **Sampled every second**, and straight away when another window comes to the front (`SetWinEventHook(EVENT_SYSTEM_FOREGROUND)`).
- **Foreground app:** `GetForegroundWindow` → process → `QueryFullProcessImageName`, reported as the lowercase file name (`chrome.exe`, `MatcherKindEnum.Process`). Store apps show their windows through `ApplicationFrameHost.exe`, so for that one it reports the process of the first child window owned by another process (`EnumChildWindows`), i.e. the app itself; if there is none (the app is still starting), it reports `applicationframehost.exe`. A process it can't open counts as nothing in front.
- **Audio** (`AudioSessions`): every active output device's sessions (`IAudioSessionManager2`), keeping those that are active, not the system sounds session, louder than about -60 dB (`IAudioMeterInformation`, so an open but silent stream doesn't count) and from a process in this session. Each is reported by process name, like the foreground app. A program stays counted for 5 s after it was last heard (`RecentlyHeard`), so short pauses don't cut its time up.
- **Idle:** after 2 minutes without keyboard or mouse input (`GetLastInputInfo`), the foreground app stops counting; only programs making sound do. With nothing making sound, nothing is in use.
- **Locked** or switched away from (`SystemEvents.SessionSwitch`): only programs making sound count.
- **Browsers** (`Sampler`, with the extension's `BrowserReport`s): a browser in front counts as the website in its active tab (`MatcherKindEnum.Domain`), and a browser making sound as the websites in its tabs making sound. Tabs only count while their browser is heard, so a report alone can't add sound. Without a report from that browser, or on a page that isn't a website, the browser counts as itself. With several profiles of one browser open, the first report wins. When a tab stops playing or is closed, the browser is still held as heard for 5 s (`RecentlyHeard`) with no tab reported, so that sound counts as the browser itself for up to 5 s.
- The Helper's own windows in front (its notices) count as nothing in front.
- Each sample holds everything in use; an empty one means nothing is. The Service stamps the time.

## Notices (`NoticeForm`, `NoticeText`)
- The Service sends a `HelperNotice` when time is running out or a program was closed. They're shown on the 1-second tick.
- **Warning:** a small window in the corner of the main display, on top, that doesn't take the keyboard and closes after 15 s.
- **Closed:** a full screen over the main display, on top, until the child presses OK. Exempt apps can be used again afterwards.
- A new notice replaces the text of one still showing, so they don't pile up.

## Pipe (`PipeSender`)
- Writes each sample as one message to the Service's pipe (`HelperPipe` in Core), connecting again every 2 s while the Service isn't there. Only the newest sample waits to be sent.
- Reads the Service's notices from the same pipe, one per line; up to 16 wait to be shown.
- Opens the pipe with exactly `ReadData | WriteData | ReadAttributes | Synchronize`, which is all the Service grants the user (`HelperPipeServer.ClientRights`). Asking for more (e.g. `PipeDirection.Out`, which means `GENERIC_WRITE`) is refused.

## Browser extension (`BrowserHost`, `BrowserPipeServer`, `BrowserPipe`)
- **Native messaging host:** Chrome and Edge start the Helper's own program, as the child at Medium integrity, with the extension's origin (`chrome-extension://…/`) as the first argument (`BrowserHost.StartedByBrowser`). It then doesn't start the Helper: it reads the extension's messages from standard input (a 4-byte length, then JSON, at most 8 KB) and writes each as one message to the Helper's browser pipe. It ends when the browser closes standard input, or with exit code 1 when the Helper isn't there (2 s) or goes away. **Standard output belongs to the browser**: write nothing to it in this mode.
- **Pipe** `Skjermvette.Agent.Browser.<session id>` (pipe names are shared by all sessions), message mode, created by the Helper with the default security, which gives this user full access and other users read only. One connection per browser; the latest valid report of each is kept and forgotten when it closes. Anything that isn't a valid report closes the connection.
- **Trust:** any program of the child's can connect and send reports, or create the pipe first. It can't add time where the browser isn't in front or heard, but it can move the browser's time to another website, **including an exempt one**, or make the browser count as itself. Checking who is on each end is #54's subject.
- Registered for development with `extension/register-host.ps1` (current user, Chrome and Edge). Not yet by the installer.
