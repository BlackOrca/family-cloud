# our-live

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A small self-hosted calendar system for a household: a CalDAV backend (bundled [Radicale](https://radicale.org/)), a server with an admin UI and a JSON API, and an Android app for actually using the calendar day to day.

## Features

- CalDAV sync — the server reads and writes events against one or more CalDAV accounts (backed by Radicale by default).
- Admin UI (`/admin/*`) for managing users, CalDAV accounts, and permissions.
- JSON API consumed by the Android app.
- Android app (.NET MAUI Blazor Hybrid) for day-to-day agenda viewing and event create/edit.
- Self-hosted end to end via Docker Compose — no external services required.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/) (runs Radicale and, for `build.ps1`, the server image)
- Android SDK / emulator, if you want to build and run `OurLive.App`

## Tech stack

.NET Aspire for local orchestration, ASP.NET Core, .NET MAUI Blazor Hybrid, EF Core with SQLite, [Ical.Net](https://github.com/rianjs/ical.net) for ICS handling, MudBlazor for UI components, and [Radicale](https://radicale.org/) as the CalDAV backend.

## Projects

- **`src/OurLive.App`** — .NET MAUI Blazor Hybrid client (Android only for now). The calendar app itself: login, agenda view, event create/edit.
- **`src/OurLive.Server`** — ASP.NET Core host. Serves the admin Blazor UI (`/admin/*` — users, CalDAV accounts, permissions) and the JSON API the app talks to. Syncs events from CalDAV accounts and writes changes back.
- **`src/OurLive.Core`** — domain logic shared by the server: CalDAV client/XML handling, ICS mapping, EF Core data access, sync/write services.
- **`src/OurLive.UI`** — shared Razor component library (MudBlazor-based) used by both the App and Server projects.
- **`src/OurLive.Contracts`** — DTOs shared between `OurLive.App` and `OurLive.Server` across the API boundary.
- **`src/OurLive.AppHost`** / **`src/OurLive.ServiceDefaults`** — .NET Aspire orchestration for local dev (`aspire run`) and for generating the deployable `docker-compose.yaml`.
- **`tests/OurLive.Core.Tests`** — unit tests for `OurLive.Core`.
- **`tests/OurLive.Server.Tests`** — integration tests for `OurLive.Server`.

## Running locally

Local dev runs the server + bundled Radicale via Aspire:

```sh
cd src/OurLive.AppHost
dotnet run
```

Configure `Jwt:SigningKey` and (optionally) `SeedAdmin:UserName`/`SeedAdmin:Password` via user-secrets on `OurLive.Server` before the first run — the seed admin is only created once, against an empty database.

To run the Android app against that server, start the emulator first:

```powershell
./emulator.ps1
```

then run `OurLive.App` (`dotnet build -t:Run -f net10.0-android`) from an IDE or the CLI. The emulator reaches the host machine via `10.0.2.2`.

## Testing

```sh
dotnet test
```

Runs the unit tests (`OurLive.Core.Tests`) and integration tests (`OurLive.Server.Tests`) for the whole solution.

## Building a deployable release

```powershell
./build.ps1
```

Bumps `version.json`, builds and signs the Android APK, builds the server Docker image, and regenerates `docker-compose.yaml` at the repo root from the Aspire app host. Requires Docker to be running.

To deploy, copy `.env.example` to `.env` next to `docker-compose.yaml` and fill in the values (image tag, JWT signing key, seed admin credentials), then:

```sh
docker compose up
```

## License

This project is licensed under the [MIT License](LICENSE).

Radicale itself is a separate, unmodified third-party component (the [`tomsquest/docker-radicale`](https://hub.docker.com/r/tomsquest/docker-radicale) image, running Radicale under its own GPL-3.0 license) that this project talks to over CalDAV/HTTP as an external service. No Radicale source is vendored or distributed as part of this repository, so its GPL-3.0 license does not apply to the OurLive code itself.
