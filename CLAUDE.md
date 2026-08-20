# CLAUDE.md

Self-hosted household calendar system: a CalDAV backend (bundled Radicale), an ASP.NET Core server (admin UI + JSON API), and a .NET MAUI Blazor Hybrid Android app.

## Solution layout (`OurLive.slnx`)

- `src/OurLive.App` — .NET MAUI Blazor Hybrid client, Android only. Login, agenda view, event create/edit.
- `src/OurLive.Server` — ASP.NET Core host. Admin Blazor UI (`/admin/*`) + JSON API for the app. Syncs events to/from CalDAV.
- `src/OurLive.Core` — shared domain logic: CalDAV client/XML, ICS mapping, EF Core data access, sync services.
- `src/OurLive.UI` — shared Razor component library (MudBlazor) used by App and Server.
- `src/OurLive.Contracts` — DTOs shared between App and Server across the API boundary.
- `src/OurLive.AppHost` / `src/OurLive.ServiceDefaults` — .NET Aspire orchestration for local dev and for generating `docker-compose.yaml`.
- `tests/OurLive.Core.Tests`, `tests/OurLive.Server.Tests` — xUnit tests.
- `radicale/config` — OurLive-authored Radicale config (INI), bind-mounted into the Radicale container. Not Radicale source code.

## Tech stack

.NET 10, ASP.NET Core, .NET MAUI Blazor Hybrid (Android), EF Core + SQLite, Ical.Net for ICS parsing, MudBlazor, .NET Aspire, ASP.NET Identity + JWT bearer auth, BCrypt.Net-Next for password hashing.

## Commands

- Run server + Radicale locally: `cd src/OurLive.AppHost && dotnet run`
- Run Android app against local server: `./emulator.ps1`, then `dotnet build -t:Run -f net10.0-android` in `OurLive.App` (emulator reaches host via `10.0.2.2`)
- Tests: `dotnet test`
- Release build (bumps `version.json`, builds/signs APK, builds server Docker image, regenerates root `docker-compose.yaml`): `./build.ps1` (requires Docker running)
- Deploy: copy `.env.example` to `.env`, then `docker compose up`

## Secrets

`OurLive.Server` needs `Jwt:SigningKey` and optionally `SeedAdmin:UserName`/`SeedAdmin:Password` via user-secrets for local dev. The seed admin is created once, against an empty database.

## Radicale integration

Radicale is consumed only as an external, unmodified third-party Docker image (`tomsquest/docker-radicale`), run as its own container and talked to over CalDAV/HTTP (`src/OurLive.Core/Security/RadicaleCredentialProvisioner.cs` and friends). No Radicale source is vendored in this repo — don't go looking for it, and don't add it.

## License

MIT (see `LICENSE`). Radicale's own GPL-3.0 license applies to the upstream `tomsquest/docker-radicale` image, not to this codebase, since it's used purely as a network service and nothing from it is vendored or distributed here.
