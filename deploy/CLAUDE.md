# Deploy (Docker)

Loaded when working in `deploy/`. General rules are in the root `CLAUDE.md`; the server's settings and hosting notes are in `src/Skjermvette.Server/CLAUDE.md`. The steps for self-hosters are in the README (Installation → Server and dashboard): keep them in step with these files.

- `Dockerfile`: three stages, built from the repository root (`.dockerignore` there): Node 22 builds `web/` into the server's `wwwroot`, the .NET 10 SDK publishes the server, and `aspnet:10.0` runs it as the non-root `app` user on port 8080. The runtime must be 10.0.13 or later (#39), so updates rebuild with `--pull`.
- `docker-compose.yml`: the server and `postgres:17`. Volumes `db` (the database) and `keys` (ASP.NET Core data protection keys, so a new container doesn't sign everyone out or break emailed links).
  - The server port is published on `127.0.0.1` only. `cloudflared` runs on the host, not in compose (decided).
  - The network has a fixed subnet (`x-network`), passed to the server as `App__ProxyNetwork`: only requests from it may set `CF-Connecting-IP` and `X-Forwarded-Proto` (#6).
  - The compose file sets the database settings. `.env` (gitignored, from `.env.example`) holds `POSTGRES_PASSWORD`, `App__PublicUrl` and the email settings.
- **Not run yet:** the dev machine has no Docker, so the files are written but untested. Unknowns to check on a real server: the image builds; the container sees cloudflared's requests from the network's gateway (no `Unknown proxy` debug logs); Cloudflare sends `X-Forwarded-Proto: https` through the tunnel; the `keys` volume is writable by `app`.
