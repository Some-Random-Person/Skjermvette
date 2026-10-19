# Windows agent: Service

Loaded when working in `src/Skjermvette.Agent.Service/`. General rules and the C# code style are in the root `CLAUDE.md`; what the agent must report and enforce is in `docs/plan.md` → "Rules the agents must follow".

## Shape
- .NET Worker Service, `net10.0-windows`. Runs as a Windows Service (LocalSystem) or in a console for testing (`dotnet run`, Development).
- **Commands** (`Commands.cs`, run as an administrator instead of the service): `pair <code> --server <url> [--name <name>]` and `forget`. In Development they don't need admin.
- `SyncWorker` runs `DeviceSync.TickAsync` every 15 s:
  1. moves usage from `UsageTracker` into the outbox;
  2. reads rules and the app catalog (`/api/device/state`) at the start and every 5 minutes, since mappings aren't pushed;
  3. uploads the outbox in batches of 500 (an empty batch when there's nothing new, which still renews the offline allowance);
  4. keeps the SignalR hub connected; pushes update `BudgetState` between check-ins.
- `HelperPipeServer` receives samples from the Helper and passes them to the `Enforcer` (see below).
- `HelperWorker` keeps the Helper running (see below).
- `BudgetState` holds what the device may use now (`Online`, `Offline` or `Unpaired`, plus a `BudgetSnapshot`) and the family's app catalog (`Apps`, an `AppResolver`).
- **Unreachable server** (`HttpRequestException`, including timeouts and 5xx) means offline: the budget comes from the cached allowance via `BudgetCalculator.CalculateOffline`. An allowance used up or expired blocks everything that isn't exempt. A device that has never checked in (no allowance yet, e.g. paired but the service never reached the server) blocks nothing, so a computer that isn't set up properly can't be locked.
- **401** means unpaired: the service logs it and stops with exit code 1. **400** on an upload drops that batch with an error in the log, since resending can't succeed.

## Data (`AgentFolder`)
- `%ProgramData%\Skjermvette\Agent`, open only to SYSTEM and Administrators (ACL set on creation). Development uses `%LOCALAPPDATA%\Skjermvette\Agent` without the ACL (`Agent:DataFolder`, `Agent:RestrictFolder`).
- `device.dat`: the `DeviceIdentity` (server, ids, token), encrypted with DPAPI in **machine** scope (pairing runs as an admin, the service as SYSTEM). The ACL is what keeps other users out.
- `outbox.db`: SQLite (EF Core, `EnsureCreated`, no pooling) with events not uploaded yet. Uploaded events are deleted, so the outbox is also "usage since the last sync", which is what the offline allowance is counted against.
- `budget.json`: catalog, exempt apps and the last allowance, so a restart while offline keeps counting.
- The allowance's `GrantedAt` is replaced with the device's own time at the check-in, so offline counting doesn't depend on the server's clock.

## Usage events (`UsageTracker`)
- Fed with samples of everything in use (foreground app or tab, plus audio), about once a second, by the Helper. Each key gets its own events; the server merges overlaps.
- A sample is stamped with the service's own clock when it arrives; the Helper sends no times.
- A key missing from samples for more than 5 s has stopped. Ongoing use is cut into a new event at every check-in, so events are short and safe to resend.

## Enforcement (`Enforcer`)
- Runs on every sample, against the latest `BudgetState` (renewed at each 15-second check-in and on pushes, so a limit can be passed by up to about 15 s). Nothing is enforced before the first budget arrives.
- **Warnings** at 5 and 1 minute left, once each per app (or for the daily total), for what is in use. Which one is named: the app, when its own limit runs out before the total. A threshold is forgotten once there's more time again (extra time, a new day).
- **Closing:** a program in use (in front or making sound) whose time is up is closed, every process with that name in the console session (`Process.Kill`, as SYSTEM). Programs only running in the background aren't counted and are left alone. Websites (domain keys) can't be closed here; the extension will block them.
- When the total is used up, unmapped programs are closed too. Exempt apps and Windows itself (`AlwaysAllowed`: the desktop, Start, Settings, the lock screen, the Helper) never are.
- Each warning, and each closing that closed something, is sent to the Helper as a `HelperNotice`.

## Helper pipe (`HelperPipeServer`)
- Named pipe `Skjermvette.Agent`, message mode, one message per sample (`HelperPipe` in Core: a JSON array of `UsageKey`, at most 8 KB and 32 keys). Anything else disconnects the client.
- The Service answers on the same pipe with `HelperNotice`s, one JSON object per line, since the user can't switch the pipe to message mode for reading.
- **ACL:** SYSTEM and the service's own account (the same once installed; the developer in a console) get full control. The user signed in at the console (`ConsoleSession.User()`, not Remote Desktop) gets only `ClientRights` (connect, read and write), not `GENERIC_WRITE`, which would let them create instances of the pipe and pose as the service.
- An instance is always open, so the name can't be taken while the service runs. When the console user changes, every instance is closed and the pipe is created again (`FirstPipeInstance`) for the new user.
- **Only the Helper the Service started** (`HelperProcess`): the client's process id (`GetNamedPipeClientProcessId`) must be the one `HelperWorker` started, or it's disconnected, so another program can't send made-up samples (e.g. empty ones). A Helper that connects before its id is recorded is refused once and connects again 2 s later. In a console (not SYSTEM) the Helper is started by hand, so any client is taken. A refused instance is closed only once the next one is open, so the name is never free.
- One Helper at a time: a new connection replaces the previous one.

## Starting the Helper (`HelperWorker`)
- Every second: when a user is signed in at the console (`ConsoleSession`), the Helper runs in their session, started as them with `WTSQueryUserToken` + `CreateProcessAsUser` on `winsta0\default` (`HelperLauncher`). Only works as SYSTEM: in a console (Development) it logs that and the Helper is started by hand.
- **High integrity** (#60): the user's token is copied and raised to High (`S-1-16-12288`) before starting the Helper. The child's programs run at Medium, and Windows doesn't let a lower level close, pause, change the permissions of, write into or send window messages to a higher one, even of the same user (as with programs run as administrator). The Helper is still the child's account, with no admin rights.
- Its environment is the user's without `DOTNET_*`, `COREHOST_*`, `CORECLR_*`, `COMPlus_*` and `COR_*` (`WithoutRuntimeSettings`). Otherwise e.g. `DOTNET_STARTUP_HOOKS` set by the child would load their own code into it at High (also when self-contained, as installed), or `DOTNET_ROOT` a runtime of their own (when not).
- It's started with `--service` (`HelperPipe.ServiceArgument`), so it skips its mutex: any program could take that name first and keep it from starting.
- Where: `Agent:HelperPath`, or `Helper\Skjermvette.Agent.Helper.exe` below the service's folder (the installer puts it there). Its own folder, since published self-contained the two programs' copies of some runtime files differ. Every user who can sign in must be able to read that folder (e.g. Program Files): started from a user's profile, it fails for other users.
- The Helper's `Process` comes from `GetProcessById`, so it has no `ExitCode` (reading it throws).
- When to start, wait or close is decided by `HelperSupervisor`, which needs no Windows:
  - a Helper that stops after running at least 30 s (e.g. the child killed it) is started again straight away;
  - one that keeps stopping sooner, or can't be started, is retried after 1 s, doubling up to 30 s;
  - when another user signs in at the console, the old Helper is closed and one is started for the new user. While the session is switching (nobody at the console), it's left running.
- Before starting, any Helper already in that session (left from before, or started by hand) is closed, so there's one per session. The Helper is closed when the service stops.
- **Tried as SYSTEM** (#60, 2026-10-19): the Helper runs at High (0x3000) and still tracks and shows both notices; `taskkill /F` and `Stop-Process` from the user's Medium shell get "Access is denied"; a program of the user's connecting to the pipe is disconnected within about 0.5 s (logged as a warning) while the Helper keeps counting. The cleaned environment wasn't tried with a hostile variable.
- **Tried as SYSTEM** (installed by hand, 2026-10-16): starting the Helper in the user's session, it connecting to the pipe, switching to another user (old Helper closed, new one started), restarting it after a kill (0.2 s after a long run, 2 s after a quick one) and closing it when the service stops.

## Tests (`tests/Skjermvette.Agent.Tests`)
- `AgentHarness` runs a paired agent against the real server in memory (`ServerFactory` from the server tests), on the server's `TestClock`, with a switch that makes the network fail like an unreachable server.
- `HelperPipeServerTests` runs the Helper's `PipeSender` against `HelperPipeServer` over a real pipe (unique name per test), as the signed-in user.
- `EnforcerTests` passes the `Enforcer` a fake for closing programs, so nothing is killed.
- `HelperLauncherTests` cover only which environment variables are left out; starting the Helper needs SYSTEM.
- `HelperSupervisorTests` drive `HelperSupervisor` with the server tests' `TestClock`; starting a process as another user isn't tested (it needs SYSTEM).
- `ServerClient` takes a handler factory for this only: SignalR disposes the handler it's given, so each use gets a new one.
