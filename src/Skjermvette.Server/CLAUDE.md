# Server (ASP.NET Core)

Loaded when working in `src/Skjermvette.Server/`. General rules and the C# code style are in the root `CLAUDE.md`.

## Shape
- ASP.NET Core **minimal API**, plus a SignalR `BudgetHub` (`/hubs/budget`) that pushes each device its `BudgetSnapshot`: on connect, and after every change on any device of the child.
- Endpoints live in `Endpoints/*Endpoints.cs` as static `Map…Endpoints` extensions; DTOs live in `Contracts/`. Device API types are shared, in `Skjermvette.Core.DeviceApi`.
- **Keys:** Guid primary keys, since devices generate event ids offline.
- **Error handling:** built-in ProblemDetails (`AddProblemDetails()`, `TypedResults.Problem` / `ValidationProblem`). No result-wrapper type. Malformed requests are 400, not 500.
- **JSON:** enums are serialized as strings, over HTTP and over SignalR.
- Serves the built dashboard from `wwwroot` (gitignored), with an `index.html` fallback for page URLs. Unknown `/api/...` paths stay 404.

## Database
- EF Core code-first. SQLite in development and tests, Postgres in production.
- Migrations are generated **for Postgres only** (`PostgresDesignTimeFactory`) and applied at startup. The SQLite dev and test database is created from the model with `EnsureCreated`, so delete `skjermvette.dev.db` after model changes.
- Add a migration: `dotnet ef migrations add <Name> --project src/Skjermvette.Server --output-dir Data/Migrations` (`dotnet tool restore` first). To remove an unapplied one without a database: `dotnet ef migrations remove --force`.
- All `DateTime`s are UTC (`...Utc` names). SQLite can't compare `DateTimeOffset` in queries, so entities use `DateTime`.
- Relationships are configured with the fluent API in `OnModelCreating`. Give strings a `HasMaxLength`.

## DI and config
- The DbContext is registered with `AddDbContext` (scoped). Anything that uses it is `AddScoped`, never a singleton. Background jobs create their own scope.
- Use the registered `TimeProvider`, never `DateTime.Now`/`UtcNow`, so tests can control time.
- **Production:** environment variables only: `ConnectionStrings__DefaultConnection`, `Database__Provider` (`Sqlite` | `Postgres`), `App__PublicUrl`, and email (`Email__From`, `Email__Smtp__Host|Port|User|Password`), plus the optional `App__ProxyNetwork`. Fail fast at startup if any required one is missing or invalid. With Docker, `deploy/docker-compose.yml` sets the database ones and a gitignored `deploy/.env` holds the rest (`deploy/.env.example` is committed).
- **Development:** `appsettings.Development.json` points at a local SQLite file and writes emails to `src/Skjermvette.Server/emails/` (gitignored) instead of sending them, so `dotnet run` works without a `.env`. Email links point at the Vite dev server (`http://localhost:5173`).
- **Hosting:** behind a Cloudflare Tunnel (`cloudflared` on the host, TLS at Cloudflare), with the container port published on `127.0.0.1` only (`deploy/`).
  - **Forwarded headers (#6):** `UseForwardedHeaders` (first in the pipeline) takes the client IP from `CF-Connecting-IP` and the scheme from `X-Forwarded-Proto`, trusted only from `App__ProxyNetwork` (the compose network, whose gateway cloudflared's requests arrive from) or localhost. So `RemoteIpAddress`, and the per-IP rate limits, are the real client's. In `TestServer` the remote address is null, which the middleware trusts, so tests can set `CF-Connecting-IP`.
  - **Cookies are `Secure` outside Development**, since the server only sees plain HTTP. `ServerFactory` therefore uses an `https://localhost` base address.

## Auth
- **Parents:** ASP.NET Core Identity with cookie auth. Optional `DisplayName` (max 100).
  - Sign-ins are **re-checked against the database every minute** (security stamp, `ParentSession.RecheckInterval`). A password or email change, or a deleted account, signs other browsers out within a minute. Anything that should cut off a parent's access (e.g. removing them from a family, #29) must call `UpdateSecurityStampAsync`.
  - The `family_id` claim is added by `ParentClaimsFactory`, so it survives the re-check. Don't add claims by hand at sign-in; use `ParentSession.SignInAsync`.
  - **Passwords:** PBKDF2-HMAC-SHA512 with a random 128-bit salt, at **250,000 iterations** (`ParentSession.PasswordHashIterations`; Identity's default is 100,000 and OWASP recommends at least 210,000). Older hashes are upgraded at the next sign-in, which also changes the security stamp.
  - **Two-factor sign-in (always on, #33):** `/api/auth/login` checks the password, then either signs in on a remembered browser or answers which second step is needed (`App` or `Email`; an emailed code is sent straight away). `/api/auth/login/code` finishes it. In between, the parent is held in Identity's two-factor cookie (15 min). `ParentSignInManager` makes every parent need a second step; `TwoFactorEnabled` means "authenticator app is on".
    - **Emailed codes** (`EmailCodeTokenProvider`, replacing Identity's `Email` provider): 6 random digits, single use, 10 minutes, only the newest works. The hash and expiry are stored with the parent's Identity tokens (no extra table), and expiry uses `TimeProvider`. Sending one again (`/api/auth/login/email-code`, also "email me a code instead" with an app) shares `EmailRateLimit`.
    - **Authenticator app** (Settings → Security, `/api/account/two-factor/...`): the key as text, an `otpauth://` link and a QR code (QRCoder, SVG). It's on once a code from it is confirmed, which also gives 10 recovery codes. Removing it needs the current password and an app or recovery code; that makes a new key and drops the recovery codes. An emailed code still works with the app on (the fallback for a lost phone).
    - Wrong codes, including recovery codes, count toward the 5-attempt lockout. Turning the app on or off, using a recovery code, and "forget all browsers" change the security stamp, which signs other browsers out.
    - Turning the app on or off and making new recovery codes each email the parent a notice with a reset link ("this wasn't me").
    - **"Remember this browser"**: 30 days (`ParentSession.RememberBrowserLifespan`), via Identity's remember-me cookie, which is checked against the security stamp, so a password change, a 2FA change or "forget all browsers" forgets it. There's no per-browser list.
    - Tests: `ServerFactory.SignInAsync` does both steps, with a code from `NewEmailCodeAsync`. `Emails()` leaves code emails out; read them with `WaitForCodeAsync`. `Totp.Code` computes an app code.
  - **Email confirmation:** a new account can't sign in until its address is confirmed. Signing in with the right password on an unconfirmed address is 403, and the dashboard offers to send the link again (`/api/auth/resend-confirmation`). A password reset also confirms the address, since the link went to it. Opening a confirmation link twice isn't an error.
  - **Registering with a taken address** gets the same reply as a new one, after the same password checks and hashing (Identity does both before it checks the address), and that address is emailed "you already have an account" instead.
  - **Forgot password** (`/api/auth/forgot-password`, then `/reset-password` with the emailed token): the reply is the same whether or not the address has an account, and the endpoints that email any address typed in (register, resend confirmation, forgot password) share a rate limit (`AuthEndpoints.EmailRateLimit`, per client IP). A reset signs other browsers out, lifts a lockout and makes the link single-use (it changes the security stamp).
  - **Links in emails** carry Identity tokens, Base64Url-encoded, and point at dashboard pages that post them back. Confirmation links work for 24 hours, reset links for 1 hour (`ParentSession.*LinkLifespan`, `PasswordResetTokenProvider`). Identity checks expiry against the system clock, not `TimeProvider`.
  - **Account settings** (`/api/account/name|email|password`): email and password changes need the current password, and wrong attempts count toward the 5-attempt lockout. `/api/auth/me` reads the user from the database.
    - **Email change:** nothing changes until the link sent to the new address is opened (`/api/auth/confirm-email-change`), which also signs other browsers out. The current address is told about the request, with a reset link: a reset cancels the change, since the confirmation link depends on the security stamp.
    - **Password change or reset:** the parent gets a "password changed" email with a reset link ("this wasn't me").
  - **Deleting an account** (`DELETE /api/account`, the current password and a second-step code in the body: from the app, a recovery code, or one emailed by `POST /api/account/deletion/email-code` (the same "Your Skjermvette code" email as at sign-in), rate limited; wrong codes count toward the lockout): the user is deleted, and if no other parent is left, the family and everything under it too, in one transaction. The database cascades from the family, except the family's own apps, which are deleted first. A new table that belongs to a family must cascade from it, and `AccountDeletionTests` must count it. `GET /api/account/deletion` tells the dashboard what will be deleted.
- **Devices:** a 6-digit pairing code (rate limited) is exchanged for a long-lived device token. Only the token's SHA-256 hash is stored. Unpaired devices have `UnpairedAtUtc` set and are left out everywhere.
- Every parent query is scoped by the family id claim (`user.GetFamilyId()`).

## Email
- **MailKit**, behind `EmailOutbox`: a background queue, so requests don't wait for SMTP and their timing doesn't reveal whether an email was sent. Emails still queued at shutdown are lost.
- SMTP is always encrypted: port 465 uses TLS from the start, any other port requires STARTTLS. Without `Email__Smtp__Host`, `Email__PickupDirectory` writes `.eml` files instead (development and tests).
- `EmailSettings` reads and checks the configuration. `App__PublicUrl` is the start of every link, since the request URL isn't the public one behind the tunnel.
- Texts live in `Email/emails.en.json` (embedded, `{name}` placeholders, plain text) so they can be translated. `AccountEmails` builds and queues the account emails.
- Tests: `ServerFactory` points the pickup directory at a temp folder. `WaitForEmailsAsync` and `LinkIn` read the emails and their links (code emails are left out). `CreateParentAsync` confirms the address directly in the database.

## Device API
- `POST /api/device/usage`: batched events, idempotent by client event GUID, each at most 1 h. Every call also **renews the offline allowance** and pushes budgets to the child's devices. Agents call it about every 15 s.
- `POST /api/device/lease`: renew without uploading. `GET /api/device/state`: rules, catalog, budget and allowance.
- Budget changes per child are serialized in memory (`ChildLocks`), which is valid for **one server instance**.

## API docs (development only)
- `Microsoft.AspNetCore.OpenApi` generates `/openapi/v1.json`; `Swashbuckle.AspNetCore.SwaggerUI` shows it at **`/docs`**.
- `Endpoints/OpenApiSetup.cs` adds the title, the device-token scheme (the Authorize button) and each endpoint handler's XML `<summary>`, read from the build's XML documentation file. **Keep a `<summary>` on every endpoint handler.**

## Tests (`tests/Skjermvette.Server.Tests`)
- `ServerFactory` runs the real server on a throwaway SQLite file **without connection pooling**. Never call `SqliteConnection.ClearAllPools()`: it's process-wide and breaks tests running in parallel (#34).
- `TestClock` controls time. Advance it to test the day reset, expiry, or the sign-in re-check.
- Server errors from test runs are written to `%TEMP%\skjermvette-test-server-errors.log` (`ServerErrorLog`).
