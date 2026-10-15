# Server (ASP.NET Core)

Loaded when working in `src/ScreenTime.Server/`. General rules and the C# code style are in the root `CLAUDE.md`.

## Shape
- ASP.NET Core **minimal API**, plus a SignalR `BudgetHub` (`/hubs/budget`) that pushes each device its `BudgetSnapshot`: on connect, and after every change on any device of the child.
- Endpoints live in `Endpoints/*Endpoints.cs` as static `Map…Endpoints` extensions; DTOs live in `Contracts/`. Device API types are shared, in `ScreenTime.Core.DeviceApi`.
- **Keys:** Guid primary keys, since devices generate event ids offline.
- **Error handling:** built-in ProblemDetails (`AddProblemDetails()`, `TypedResults.Problem` / `ValidationProblem`). No result-wrapper type. Malformed requests are 400, not 500.
- **JSON:** enums are serialized as strings, over HTTP and over SignalR.
- Serves the built dashboard from `wwwroot` (gitignored), with an `index.html` fallback for page URLs. Unknown `/api/...` paths stay 404.

## Database
- EF Core code-first. SQLite in development and tests, Postgres in production.
- Migrations are generated **for Postgres only** (`PostgresDesignTimeFactory`) and applied at startup. The SQLite dev and test database is created from the model with `EnsureCreated`, so delete `screentime.dev.db` after model changes.
- Add a migration: `dotnet ef migrations add <Name> --project src/ScreenTime.Server --output-dir Data/Migrations` (`dotnet tool restore` first). To remove an unapplied one without a database: `dotnet ef migrations remove --force`.
- All `DateTime`s are UTC (`...Utc` names). SQLite can't compare `DateTimeOffset` in queries, so entities use `DateTime`.
- Relationships are configured with the fluent API in `OnModelCreating`. Give strings a `HasMaxLength`.

## DI and config
- The DbContext is registered with `AddDbContext` (scoped). Anything that uses it is `AddScoped`, never a singleton. Background jobs create their own scope.
- Use the registered `TimeProvider`, never `DateTime.Now`/`UtcNow`, so tests can control time.
- **Production:** environment variables only (`ConnectionStrings__DefaultConnection`, `Database__Provider` = `Sqlite` | `Postgres`). Fail fast at startup if either is missing. A gitignored `.env` holds the values, and `.env.example` is committed.
- **Development:** `appsettings.Development.json` points at a local SQLite file, so `dotnet run` works without a `.env`.
- **Hosting:** behind a Cloudflare Tunnel (`cloudflared` on the host, TLS at Cloudflare, client IP in `CF-Connecting-IP` / `X-Forwarded-For`), with the container port published on `127.0.0.1` only. Forwarded headers aren't handled yet (#6).

## Auth
- **Parents:** ASP.NET Core Identity with cookie auth. Optional `DisplayName` (max 100).
  - Sign-ins are **re-checked against the database every minute** (security stamp, `ParentSession.RecheckInterval`). A password or email change, or a deleted account, signs other browsers out within a minute. Anything that should cut off a parent's access (e.g. removing them from a family, #29) must call `UpdateSecurityStampAsync`.
  - The `family_id` claim is added by `ParentClaimsFactory`, so it survives the re-check. Don't add claims by hand at sign-in; use `ParentSession.SignInAsync`.
  - **Passwords:** PBKDF2-HMAC-SHA512 with a random 128-bit salt, at **250,000 iterations** (`ParentSession.PasswordHashIterations`; Identity's default is 100,000 and OWASP recommends at least 210,000). Older hashes are upgraded at the next sign-in, which also changes the security stamp.
  - **Account settings** (`/api/account/name|email|password`): email and password changes need the current password, and wrong attempts count toward the 5-attempt lockout. There's **no confirmation email** (the server doesn't send email yet, #27). `/api/auth/me` reads the user from the database.
  - **Deleting an account** (`DELETE /api/account`, current password in the body): the user is deleted, and if no other parent is left, the family and everything under it too, in one transaction. The database cascades from the family, except the family's own apps, which are deleted first. A new table that belongs to a family must cascade from it, and `AccountDeletionTests` must count it. `GET /api/account/deletion` tells the dashboard what will be deleted.
- **Devices:** a 6-digit pairing code (rate limited) is exchanged for a long-lived device token. Only the token's SHA-256 hash is stored. Unpaired devices have `UnpairedAtUtc` set and are left out everywhere.
- Every parent query is scoped by the family id claim (`user.GetFamilyId()`).

## Device API
- `POST /api/device/usage`: batched events, idempotent by client event GUID, each at most 1 h. Every call also **renews the offline allowance** and pushes budgets to the child's devices. Agents call it about every 15 s.
- `POST /api/device/lease`: renew without uploading. `GET /api/device/state`: rules, catalog, budget and allowance.
- Budget changes per child are serialized in memory (`ChildLocks`), which is valid for **one server instance**.

## API docs (development only)
- `Microsoft.AspNetCore.OpenApi` generates `/openapi/v1.json`; `Swashbuckle.AspNetCore.SwaggerUI` shows it at **`/docs`**.
- `Endpoints/OpenApiSetup.cs` adds the title, the device-token scheme (the Authorize button) and each endpoint handler's XML `<summary>`, read from the build's XML documentation file. **Keep a `<summary>` on every endpoint handler.**

## Tests (`tests/ScreenTime.Server.Tests`)
- `ServerFactory` runs the real server on a throwaway SQLite file **without connection pooling**. Never call `SqliteConnection.ClearAllPools()`: it's process-wide and breaks tests running in parallel (#34).
- `TestClock` controls time. Advance it to test the day reset, expiry, or the sign-in re-check.
- Server errors from test runs are written to `%TEMP%\screentime-test-server-errors.log` (`ServerErrorLog`).
