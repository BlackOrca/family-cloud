# CLAUDE.md

Self-hosted household calendar system: a CalDAV backend (bundled Radicale), an ASP.NET Core server (admin UI + JSON API), and a .NET MAUI Blazor Hybrid Android app.

## Solution layout (`FamilyCloud.slnx`)

Organized as a modular monolith: one `FamilyCloud.<Feature>` project per fachliche Domäne (Domain + Data config + API endpoints, server-only), composed together by `FamilyCloud.Server`. See the Phase 1 architecture roadmap for the full reasoning.

- `src/FamilyCloud.App` — .NET MAUI Blazor Hybrid client, Android only. Login, agenda view, event create/edit, account self-service.
- `src/FamilyCloud.Server` — ASP.NET Core host and composition root. Admin Blazor UI (`/admin/*`, `/account`) + owns the composed `FamilyCloudDbContext` (`Data/`) that every feature project's entities are configured on.
- `src/FamilyCloud.Calendar` — the Calendar feature: CalDAV client/XML, ICS mapping, Radicale credential provisioning, sync services, `/api/calendars`+`/api/events` endpoints. Referenced only by Server.
- `src/FamilyCloud.Family` — the Family/User-management feature: Family/FamilyMember domain, login (issues the JWT, including family/role claims), account self-service endpoints (`/api/account/*`), `/api/family/members`. Referenced only by Server.
- `src/FamilyCloud.Core` — cross-feature infrastructure only: `AppUser` (Identity base every feature's permission rows reference by `Guid UserId`), generic sync primitives (`SyncEvent`/`SyncEventPublisher`, used by every feature's write paths), `ClaimsPrincipalExtensions`/`FamilyClaimTypes`.
- `src/FamilyCloud.UI` — shared Razor component library (MudBlazor) used by App and Server.
- `src/FamilyCloud.Contracts` — DTOs shared between App and Server across the API boundary, organized by feature folder.
- `src/FamilyCloud.AppHost` / `src/FamilyCloud.ServiceDefaults` — .NET Aspire orchestration (including the PostgreSQL resource) for local dev and for generating `docker-compose.yaml`.
- `tests/FamilyCloud.Calendar.Tests`, `tests/FamilyCloud.Family.Tests`, `tests/FamilyCloud.Core.Tests`, `tests/FamilyCloud.Server.Tests` — xUnit tests, mirroring the `src/` feature split.
- `radicale/config` — FamilyCloud-authored Radicale config (INI), bind-mounted into the Radicale container. Not Radicale source code.

New feature domains (Todos, Photos, Storage, ...) follow the same pattern: a new `FamilyCloud.<Feature>` project referenced only by Server, its own `Contracts/<Feature>/` DTO folder, its own resource-scoped `<Feature>Permission` table (family-scoped via a `FamilyId` on the owning resource), and an `Add<Feature>Feature()`/`Map<Feature>Endpoints()` pair wired up in `Program.cs`.

## Tech stack

.NET 10, ASP.NET Core, .NET MAUI Blazor Hybrid (Android), EF Core + PostgreSQL (production) / SQLite (test-only fallback, see `Database:Provider`), Ical.Net for ICS parsing, MudBlazor, .NET Aspire, ASP.NET Identity + JWT bearer auth, BCrypt.Net-Next for password hashing.

## Commands

- Run server + Radicale locally: `cd src/FamilyCloud.AppHost && dotnet run`
- Run Android app against local server: `./emulator.ps1`, then `dotnet build -t:Run -f net10.0-android` in `FamilyCloud.App` (emulator reaches host via `10.0.2.2`)
- Tests: `dotnet test`
- Release build (bumps `version.json`, builds/signs APK, builds server Docker image, regenerates root `docker-compose.yaml`): `./build.ps1` (requires Docker running)
- Deploy: copy `.env.example` to `.env`, then `docker compose up`

## Secrets

`FamilyCloud.Server` needs `Jwt:SigningKey` and optionally `SeedAdmin:UserName`/`SeedAdmin:Password` via user-secrets for local dev. The seed admin is created once, against an empty database.

## Radicale integration

Radicale is consumed only as an external, unmodified third-party Docker image (`tomsquest/docker-radicale`), run as its own container and talked to over CalDAV/HTTP (`src/FamilyCloud.Core/Security/RadicaleCredentialProvisioner.cs` and friends). No Radicale source is vendored in this repo — don't go looking for it, and don't add it.

## License

MIT (see `LICENSE`). Radicale's own GPL-3.0 license applies to the upstream `tomsquest/docker-radicale` image, not to this codebase, since it's used purely as a network service and nothing from it is vendored or distributed here.
