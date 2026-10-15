# Windows agent: Service

Loaded when working in `src/ScreenTime.Agent.Service/`. General rules and the C# code style are in the root `CLAUDE.md`; what the agent must report and enforce is in `docs/plan.md` → "Rules the agents must follow".

## Shape
- .NET Worker Service, `net10.0-windows`. Runs as a Windows Service (LocalSystem) or in a console for testing (`dotnet run`, Development).
- **Commands** (`Commands.cs`, run as an administrator instead of the service): `pair <code> --server <url> [--name <name>]` and `forget`. In Development they don't need admin.
- `SyncWorker` runs `DeviceSync.TickAsync` every 15 s:
  1. moves usage from `UsageTracker` into the outbox;
  2. reads rules and the app catalog (`/api/device/state`) at the start and every 5 minutes, since mappings aren't pushed;
  3. uploads the outbox in batches of 500 (an empty batch when there's nothing new, which still renews the offline allowance);
  4. keeps the SignalR hub connected; pushes update `BudgetState` between check-ins.
- `BudgetState` holds what the device may use now (`Online`, `Offline` or `Unpaired`, plus a `BudgetSnapshot`). Enforcement will read it.
- **Unreachable server** (`HttpRequestException`, including timeouts and 5xx) means offline: the budget comes from the cached allowance via `BudgetCalculator.CalculateOffline`. No allowance, or an expired one, blocks everything that isn't exempt.
- **401** means unpaired: the service logs it and stops with exit code 1. **400** on an upload drops that batch with an error in the log, since resending can't succeed.

## Data (`AgentFolder`)
- `%ProgramData%\ScreenTime\Agent`, open only to SYSTEM and Administrators (ACL set on creation). Development uses `%LOCALAPPDATA%\ScreenTime\Agent` without the ACL (`Agent:DataFolder`, `Agent:RestrictFolder`).
- `device.dat`: the `DeviceIdentity` (server, ids, token), encrypted with DPAPI in **machine** scope (pairing runs as an admin, the service as SYSTEM). The ACL is what keeps other users out.
- `outbox.db`: SQLite (EF Core, `EnsureCreated`, no pooling) with events not uploaded yet. Uploaded events are deleted, so the outbox is also "usage since the last sync", which is what the offline allowance is counted against.
- `budget.json`: catalog, exempt apps and the last allowance, so a restart while offline keeps counting.
- The allowance's `GrantedAt` is replaced with the device's own time at the check-in, so offline counting doesn't depend on the server's clock.

## Usage events (`UsageTracker`)
- Fed with samples of everything in use (foreground app or tab, plus audio), about once a second, by the Helper (next step). Each key gets its own events; the server merges overlaps.
- A key missing from samples for more than 5 s has stopped. Ongoing use is cut into a new event at every check-in, so events are short and safe to resend.

## Tests (`tests/ScreenTime.Agent.Tests`)
- `AgentHarness` runs a paired agent against the real server in memory (`ServerFactory` from the server tests), on the server's `TestClock`, with a switch that makes the network fail like an unreachable server.
- `ServerClient` takes a handler factory for this only: SignalR disposes the handler it's given, so each use gets a new one.
