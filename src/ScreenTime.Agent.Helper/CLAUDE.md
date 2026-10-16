# Windows agent: Helper

Loaded when working in `src/ScreenTime.Agent.Helper/`. General rules and the C# code style are in the root `CLAUDE.md`; the Service it reports to is described in `src/ScreenTime.Agent.Service/CLAUDE.md`.

## Shape
- WinForms, `net10.0-windows`, no window of its own (`HelperContext`, an `ApplicationContext`). It runs in the child's session because a SYSTEM service can't see the foreground window or show UI.
- One per session (a `Local\` mutex). Debug builds are console apps that print what's in use; Release builds are `WinExe`. For now it's started by hand (`dotnet run --project src/ScreenTime.Agent.Helper`); the Service will launch it later.
- Win32 calls live in `NativeMethods` (`DllImport`), WASAPI's COM interfaces in `AudioSessions`. Anything that doesn't need Windows goes in `Sampler` or `RecentlyHeard`, so it can be unit tested.

## What it reports
- **Sampled every second**, and straight away when another window comes to the front (`SetWinEventHook(EVENT_SYSTEM_FOREGROUND)`).
- **Foreground app:** `GetForegroundWindow` → process → `QueryFullProcessImageName`, reported as the lowercase file name (`chrome.exe`, `MatcherKindEnum.Process`). A process it can't open counts as nothing in front.
- **Audio** (`AudioSessions`): every active output device's sessions (`IAudioSessionManager2`), keeping those that are active, not the system sounds session, louder than about -60 dB (`IAudioMeterInformation`, so an open but silent stream doesn't count) and from a process in this session. Each is reported by process name, like the foreground app. A program stays counted for 5 s after it was last heard (`RecentlyHeard`), so short pauses don't cut its time up.
- **Idle:** after 2 minutes without keyboard or mouse input (`GetLastInputInfo`), the foreground app stops counting; only programs making sound do. With nothing making sound, nothing is in use.
- **Locked** or switched away from (`SystemEvents.SessionSwitch`): only programs making sound count.
- Each sample holds everything in use; an empty one means nothing is. The Service stamps the time.

## Pipe (`PipeSender`)
- Writes each sample as one message to the Service's pipe (`HelperPipe` in Core), connecting again every 2 s while the Service isn't there. Only the newest sample waits to be sent.
- Opens the pipe with exactly `WriteData | ReadAttributes | Synchronize`, which is all the Service grants the user (`HelperPipeServer.ClientRights`). Asking for more (e.g. `PipeDirection.Out`, which means `GENERIC_WRITE`) is refused.
