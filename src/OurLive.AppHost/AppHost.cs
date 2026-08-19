var builder = DistributedApplication.CreateBuilder(args);

// Only affects `aspire publish -p docker-compose` (see build.ps1) — generates docker-compose.yml
// for every resource below. Has no effect on `aspire run`/local dev.
var composeEnvironment = builder.AddDockerComposeEnvironment("ourlive");

// Radicale's local-dev named volume (see .WithVolume below) still registers a top-level entry here
// even though its per-service mount gets overridden to a bind mount below — drop it so the generated
// file declares no named volumes at all, matching the bind-mount-only data layout.
composeEnvironment.ConfigureComposeFile(compose => compose.Volumes.Clear());

// Bundled "internal" CalDAV server — OurLive.Server connects to it like any other generic CalDAV
// account (same ICalDavClient, no special-casing). Auth stays disabled (auth: none) here: this
// container is never port-published beyond local dev / the compose-internal network, consistent
// with the no-TLS/internal-network-only posture already accepted for the whole system. Revisit if
// Radicale is ever exposed beyond that trust boundary.
var radicale = builder.AddContainer("radicale", "tomsquest/docker-radicale")
    .WithHttpEndpoint(port: 5232, targetPort: 5232, name: "http")
    .WithVolume("ourlive-radicale-data", "/data")
    .WithEnvironment("TZ", "Europe/Berlin")
    // Docker Compose output: a bind mount under ./data next to docker-compose.yaml instead of a
    // Docker-managed named volume, so the data is plainly visible/backup-able alongside the deployment
    // artifact. Local dev (`aspire run`) keeps the named volume above, untouched.
    .PublishAsDockerComposeService((_, service) =>
    {
        service.Volumes.Clear();
        service.Volumes.Add(new Aspire.Hosting.Docker.Resources.ServiceNodes.Volume
        {
            Name = "radicale-data",
            Type = "bind",
            Source = "./data/radicale",
            Target = "/data",
        });
    });

builder.AddProject<Projects.OurLive_Server>("server")
    .WaitFor(radicale)
    .PublishAsDockerComposeService((_, service) =>
    {
        // The generated docker-compose.yaml only `expose`s container ports by default (reachable from
        // other compose services, not the host). The server is the one thing that has to be reachable
        // from outside the compose network — the app talks to it over the LAN — so publish it explicitly.
        // Host port fixed at 5253 to match local dev; container port stays the ${SERVER_PORT} Aspire
        // already threads through HTTP_PORTS, so both sides of the mapping move together if that ever changes.
        service.Ports.Add("5253:${SERVER_PORT}");

        // Bind mount under ./data next to docker-compose.yaml (SQLite file + Data Protection keys) —
        // same reasoning as Radicale's above. .WithVolume(...) doesn't support project resources at
        // all, so this is set directly on the compose model regardless.
        service.Volumes.Add(new Aspire.Hosting.Docker.Resources.ServiceNodes.Volume
        {
            Name = "server-data",
            Type = "bind",
            Source = "./data/server",
            Target = "/data",
        });

        // A freshly created bind-mount directory is root-owned on a real Linux host, but the aspnet
        // base image runs as a non-root user by default — without this, SQLite can't create ourlive.db
        // under /data ("unable to open database file"). Running as root is an accepted trade for
        // simplicity here, consistent with the no-TLS/internal-network-only posture already in place.
        service.User = "0:0";
    })
    // In local dev these come from user-secrets (see Program.cs); Docker Compose has no equivalent,
    // so surface them as blank .env placeholders the deployer fills in before the first `docker
    // compose up` — same pattern Aspire already uses for SERVER_IMAGE/SERVER_PORT above. Only applied
    // in publish mode so `aspire run` for local dev is untouched.
    .WithEnvironment(context =>
    {
        if (!context.ExecutionContext.IsPublishMode)
        {
            return;
        }

        context.EnvironmentVariables["ConnectionStrings__DefaultConnection"] = "Data Source=/data/ourlive.db";
        context.EnvironmentVariables["Jwt__SigningKey"] = "${JWT_SIGNING_KEY}";
        context.EnvironmentVariables["SeedAdmin__UserName"] = "${SEED_ADMIN_USERNAME}";
        context.EnvironmentVariables["SeedAdmin__Password"] = "${SEED_ADMIN_PASSWORD}";
    });

builder.Build().Run();
