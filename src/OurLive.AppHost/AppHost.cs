var builder = DistributedApplication.CreateBuilder(args);

// Only affects `aspire publish -p docker-compose` (see build.ps1) — generates docker-compose.yml
// for every resource below. Has no effect on `aspire run`/local dev.
var composeEnvironment = builder.AddDockerComposeEnvironment("ourlive");

// Radicale's local-dev named volume (see .WithVolume below) still registers a top-level entry here
// even though its per-service mount gets overridden to a bind mount below — drop it so the generated
// file declares no named volumes at all, matching the bind-mount-only data layout.
composeEnvironment.ConfigureComposeFile(compose => compose.Volumes.Clear());

// Radicale's htpasswd auth backend reads /auth/users eagerly at process startup and exits with a
// CRITICAL error if it's missing — it does NOT defer to first request the way the writable-directory
// mount below assumes. On a fresh checkout the file is only ever written later by OurLive.Server's
// RadicaleCredentialProvisioner (during admin seeding), so without this, Radicale crashes before the
// server gets a chance to write it. This one-shot container just guarantees the file exists (even
// empty — Radicale is fine booting with zero htpasswd entries) before Radicale's first boot.
var radicaleAuthInit = builder.AddContainer("radicale-auth-init", "busybox")
    .WithEntrypoint("sh")
    .WithArgs("-c", "mkdir -p /auth && [ -f /auth/users ] || touch /auth/users")
    .WithBindMount("../../.data/radicale-auth", "/auth")
    .PublishAsDockerComposeService((_, service) =>
    {
        // One-shot: must not loop-restart after it exits 0.
        service.Restart = "no";

        service.Volumes.Clear();
        service.Volumes.Add(new Aspire.Hosting.Docker.Resources.ServiceNodes.Volume
        {
            Name = "radicale-auth",
            Type = "bind",
            Source = "./data/radicale-auth",
            Target = "/auth",
        });
    });

// Bundled "internal" CalDAV server — OurLive.Server connects to it like any other generic CalDAV
// account (same ICalDavClient, no special-casing). This container is never port-published beyond
// local dev / the compose-internal network, consistent with the no-TLS/internal-network-only posture
// already accepted for the whole system. Revisit if Radicale is ever exposed beyond that trust boundary.
var radicale = builder.AddContainer("radicale", "tomsquest/docker-radicale")
    // Local dev (`aspire run`): wait for the auth file to exist before Radicale boots.
    .WaitForCompletion(radicaleAuthInit)
    .WithHttpEndpoint(port: 5232, targetPort: 5232, name: "http")
    .WithVolume("ourlive-radicale-data", "/data")
    .WithEnvironment("TZ", "Europe/Berlin")
    // Static auth/rights config, committed to the repo (radicale/config) so it's always present before
    // the container starts — Radicale reads this once at process startup, so it can't depend on
    // anything the server writes later. Enables htpasswd auth (see RadicaleCredentialProvisioner in
    // OurLive.Core) instead of the previous auth: none; rights are owner_only since the server always
    // connects as the one managed user and every household calendar lives under that user's own
    // collection tree.
    .WithBindMount("../../radicale/config", "/config/config", isReadOnly: true)
    // Writable directory for the htpasswd file the server maintains. A directory-level (not
    // single-file) mount so ongoing credential updates need no restart — Radicale's htpasswd auth
    // module rereads the file per request. The file itself must still exist by the time Radicale's
    // process starts (see radicaleAuthInit above); it doesn't defer reading it until first request.
    .WithBindMount("../../.data/radicale-auth", "/auth")
    // Docker Compose output: bind mounts under ./data and ./radicale next to docker-compose.yaml
    // instead of Docker-managed named volumes / the aspire-run-relative paths above, so the data is
    // plainly visible/backup-able alongside the deployment artifact, and restarts automatically like
    // any other long-running service on the host.
    .PublishAsDockerComposeService((_, service) =>
    {
        service.Restart = "unless-stopped";

        service.Volumes.Clear();
        service.Volumes.Add(new Aspire.Hosting.Docker.Resources.ServiceNodes.Volume
        {
            Name = "radicale-data",
            Type = "bind",
            Source = "./data/radicale",
            Target = "/data",
        });
        service.Volumes.Add(new Aspire.Hosting.Docker.Resources.ServiceNodes.Volume
        {
            Name = "radicale-config",
            Type = "bind",
            Source = "./radicale/config",
            Target = "/config/config",
            ReadOnly = true,
        });
        service.Volumes.Add(new Aspire.Hosting.Docker.Resources.ServiceNodes.Volume
        {
            Name = "radicale-auth",
            Type = "bind",
            Source = "./data/radicale-auth",
            Target = "/auth",
        });

        // Docker Compose equivalent of the .WaitForCompletion(radicaleAuthInit) above — that API has
        // no effect at deployment time (local-orchestration only), so the ordering has to be encoded
        // directly on the generated compose service too.
        service.DependsOn["radicale-auth-init"] = new Aspire.Hosting.Docker.Resources.ComposeNodes.ServiceDependency
        {
            Condition = "service_completed_successfully",
        };
    });

builder.AddProject<Projects.OurLive_Server>("server")
    .WaitFor(radicale)
    .PublishAsDockerComposeService((_, service) =>
    {
        service.Restart = "unless-stopped";

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

        // Same host directory as Radicale's /auth mount above — the server writes the htpasswd file
        // here (see RadicaleCredentialProvisioner), Radicale reads it from its own side of the mount.
        service.Volumes.Add(new Aspire.Hosting.Docker.Resources.ServiceNodes.Volume
        {
            Name = "radicale-auth",
            Type = "bind",
            Source = "./data/radicale-auth",
            Target = "/radicale-auth",
        });

        // A freshly created bind-mount directory is root-owned on a real Linux host, but the aspnet
        // base image runs as a non-root user by default — without this, SQLite can't create ourlive.db
        // under /data ("unable to open database file"). Running as root is an accepted trade for
        // simplicity here, consistent with the no-TLS/internal-network-only posture already in place.
        service.User = "0:0";
    })
    .WithEnvironment(context =>
    {
        if (context.ExecutionContext.IsPublishMode)
        {
            // In local dev these come from user-secrets (see Program.cs); Docker Compose has no
            // equivalent, so surface them as blank .env placeholders the deployer fills in before the
            // first `docker compose up` — same pattern used for SERVER_IMAGE/SERVER_PORT above.
            context.EnvironmentVariables["ConnectionStrings__DefaultConnection"] = "Data Source=/data/ourlive.db";
            context.EnvironmentVariables["Jwt__SigningKey"] = "${JWT_SIGNING_KEY}";
            context.EnvironmentVariables["SeedAdmin__UserName"] = "${SEED_ADMIN_USERNAME}";
            context.EnvironmentVariables["SeedAdmin__Password"] = "${SEED_ADMIN_PASSWORD}";

            // Both containers reach each other by compose service name on the shared network.
            context.EnvironmentVariables["Radicale__BaseUrl"] = "http://radicale:5232/";
            context.EnvironmentVariables["Radicale__HtpasswdPath"] = "/radicale-auth/users";
        }
        else
        {
            // `aspire run`: the server runs natively on the host (not containerized), so it reaches
            // Radicale via the host port Aspire publishes for it (container DNS names like "radicale"
            // aren't resolvable outside the Docker network), and writes the htpasswd file straight to
            // the same host path bind-mounted into Radicale's /auth above.
            context.EnvironmentVariables["Radicale__BaseUrl"] = "http://localhost:5232/";
            context.EnvironmentVariables["Radicale__HtpasswdPath"] = "../../.data/radicale-auth/users";
        }
    });

builder.Build().Run();
