# Windows agent: Helper

Loaded when working in `src/ScreenTime.Agent.Helper/`. General rules and the C# code style are in the root `CLAUDE.md`; the Service it reports to is described in `src/ScreenTime.Agent.Service/CLAUDE.md`.

## Shape
- WinForms, `net10.0-windows`, no window of its own (`HelperContext`, an `ApplicationContext`). It runs in the child's session because a SYSTEM service can't see the foreground window or show UI.
- One per session (a `Local\` mutex). Debug builds are console apps that print what's in use; Release builds are `WinExe`. The Service starts it in the signed-in user's session and restarts it when it stops (`HelperWorker` in the Service). In development the Service doesn't run as SYSTEM and can't, so start it by hand (`dotnet run --project src/ScreenTime.Agent.Helper`).
- Win32 calls live in `NativeMethods` (`DllImport`), WASAPI's COM interfaces in `AudioSessions`. Anything that doesn't need Windows goes in `Sampler`, `RecentlyHeard` or `NoticeText`, so it can be unit tested.

## What it reports
- **Sampled every second**, and straight away when another window comes to the front (`SetWinEventHook(EVENT_SYSTEM_FOREGROUND)`).
- **Foreground app:** `GetForegroundWindow` → process → `QueryFullProcessImageName`, reported as the lowercase file name (`chrome.exe`, `MatcherKindEnum.Process`). A process it can't open counts as nothing in front.
- **Audio** (`AudioSessions`): every active output device's sessions (`IAudioSessionManager2`), keeping those that are active, not the system sounds session, louder than about -60 dB (`IAudioMeterInformation`, so an open but silent stream doesn't count) and from a process in this session. Each is reported by process name, like the foreground app. A program stays counted for 5 s after it was last heard (`RecentlyHeard`), so short pauses don't cut its time up.
- **Idle:** after 2 minutes without keyboard or mouse input (`GetLastInputInfo`), the foreground app stops counting; only programs making sound do. With nothing making sound, nothing is in use.
- **Locked** or switched away from (`SystemEvents.SessionSwitch`): only programs making sound count.
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
