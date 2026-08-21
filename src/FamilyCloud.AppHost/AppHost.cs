var builder = DistributedApplication.CreateBuilder(args);

// Only affects `aspire publish -p docker-compose` (see build.ps1) — generates docker-compose.yml
// for every resource below. Has no effect on `aspire run`/local dev.
var composeEnvironment = builder.AddDockerComposeEnvironment("familycloud");

// Radicale's local-dev named volume (see .WithVolume below) still registers a top-level entry here
// even though its per-service mount gets overridden to a bind mount below — drop it so the generated
// file declares no named volumes at all, matching the bind-mount-only data layout.
composeEnvironment.ConfigureComposeFile(compose => compose.Volumes.Clear());

// Radicale's htpasswd auth backend reads /auth/users eagerly at process startup and exits with a
// CRITICAL error if it's missing — it does NOT defer to first request the way the writable-directory
// mount below assumes. On a fresh checkout the file is only ever written later by FamilyCloud.Server's
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

// PostgreSQL — the app database (see the Phase 1 architecture roadmap for why this replaced SQLite:
// mainly concurrent-write robustness once multiple family members and feature areas write at once).
// The database resource is named "DefaultConnection" (not e.g. "familyclouddb") purely so the
// connection string Aspire injects into the server lands under the same
// ConnectionStrings:DefaultConnection config key plain `dotnet run` and docker-compose already use.
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("familycloud-postgres-data")
    .PublishAsDockerComposeService((_, service) =>
    {
        service.Restart = "unless-stopped";

        service.Volumes.Clear();
        service.Volumes.Add(new Aspire.Hosting.Docker.Resources.ServiceNodes.Volume
        {
            Name = "postgres-data",
            Type = "bind",
            Source = "./data/postgres",
            Target = "/var/lib/postgresql/data",
        });
    });
var familyCloudDb = postgres.AddDatabase("DefaultConnection", databaseName: "familycloud");

// Bundled "internal" CalDAV server — FamilyCloud.Server connects to it like any other generic CalDAV
// account (same ICalDavClient, no special-casing). This container is never port-published beyond
// local dev / the compose-internal network, consistent with the no-TLS/internal-network-only posture
// already accepted for the whole system. Revisit if Radicale is ever exposed beyond that trust boundary.
var radicale = builder.AddContainer("radicale", "tomsquest/docker-radicale")
    // Local dev (`aspire run`): wait for the auth file to exist before Radicale boots.
    .WaitForCompletion(radicaleAuthInit)
    .WithHttpEndpoint(port: 5232, targetPort: 5232, name: "http")
    .WithVolume("familycloud-radicale-data", "/data")
    .WithEnvironment("TZ", "Europe/Berlin")
    // Static auth/rights config, committed to the repo (radicale/config) so it's always present before
    // the container starts — Radicale reads this once at process startup, so it can't depend on
    // anything the server writes later. Enables htpasswd auth (see RadicaleCredentialProvisioner in
    // FamilyCloud.Core) instead of the previous auth: none; rights are owner_only since the server always
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

// Immich — bundled third-party photo backend (unmodified image, like Radicale above), consumed only
// via FamilyCloud.Photos' IImmichClient over its REST API. See the Phase 3 architecture roadmap: a
// single shared Immich service account (the "broker" model) rather than one Immich user per family
// member — FamilyCloud controls visibility itself via PhotoAlbumPermission, the same way it already
// brokers the one shared Radicale credential for Calendar.
//
// Immich needs its own dedicated PostgreSQL — a build with a vector-search extension baked in, unlike
// the app's own plain "postgres" resource above — plus Redis for its job queue. Both are
// Immich-internal implementation details FamilyCloud.Server never talks to directly; only
// immich-server itself is reached, via IImmichClient.
var immichPostgres = builder.AddPostgres("immich-postgres")
    // AddPostgres defaults to a docker.io-registry image; .WithImage() alone would leave that default
    // Registry in place and get concatenated in front of this ghcr.io one, so it's set explicitly too.
    .WithImage("immich-app/postgres", "14-vectorchord0.4.3-pgvectors0.2.0")
    .WithImageRegistry("ghcr.io")
    .WithEnvironment("POSTGRES_DB", "immich")
    .WithDataVolume("familycloud-immich-postgres-data")
    .PublishAsDockerComposeService((_, service) =>
    {
        service.Restart = "unless-stopped";

        service.Volumes.Clear();
        service.Volumes.Add(new Aspire.Hosting.Docker.Resources.ServiceNodes.Volume
        {
            Name = "immich-postgres-data",
            Type = "bind",
            Source = "./data/immich-postgres",
            Target = "/var/lib/postgresql/data",
        });
    });

var immichRedis = builder.AddContainer("immich-redis", "docker.io/valkey/valkey", "9")
    .PublishAsDockerComposeService((_, service) => service.Restart = "unless-stopped");

// Only orchestrated for `aspire publish -p docker-compose` (see build.ps1), never for local `aspire
// run` — Immich's machine-learning container (face/object recognition) downloads several GB of models
// and is CPU/RAM-heavy, unnecessary for local dev. Enabling it later in production needs no code
// change here: Immich's own admin settings gate whether Smart Search/face recognition actually use it.
IResourceBuilder<ContainerResource>? immichMachineLearning = null;
if (builder.ExecutionContext.IsPublishMode)
{
    immichMachineLearning = builder.AddContainer("immich-machine-learning", "ghcr.io/immich-app/immich-machine-learning", "release")
        .WithVolume("familycloud-immich-model-cache", "/cache")
        .PublishAsDockerComposeService((_, service) =>
        {
            service.Restart = "unless-stopped";

            service.Volumes.Clear();
            service.Volumes.Add(new Aspire.Hosting.Docker.Resources.ServiceNodes.Volume
            {
                Name = "immich-model-cache",
                Type = "bind",
                Source = "./data/immich-model-cache",
                Target = "/cache",
            });
        });
}

// Immich itself is always a container (unlike FamilyCloud.Server, which runs natively during `aspire
// run` — see the Radicale BaseUrl branching below) — it reaches immich-postgres/immich-redis by
// container DNS name in both local dev and docker-compose, so unlike Radicale's BaseUrl there's no
// aspire-run-vs-publish split needed here.
var immichServer = builder.AddContainer("immich-server", "ghcr.io/immich-app/immich-server", "release")
    .WaitFor(immichPostgres)
    .WaitFor(immichRedis)
    .WithHttpEndpoint(port: 2283, targetPort: 2283, name: "http")
    .WithEnvironment("DB_HOSTNAME", "immich-postgres")
    .WithEnvironment("DB_USERNAME", "postgres")
    .WithEnvironment("DB_PASSWORD", immichPostgres.Resource.PasswordParameter!)
    .WithEnvironment("DB_DATABASE_NAME", "immich")
    .WithEnvironment("REDIS_HOSTNAME", "immich-redis")
    .WithEnvironment("TZ", "Europe/Berlin")
    .WithVolume("familycloud-immich-uploads", "/data")
    .WithEnvironment(context =>
    {
        // Left unset for local dev (no immich-machine-learning container — see above); Immich itself
        // handles a missing/unreachable ML backend gracefully (affected jobs just fail, logged in its
        // own admin UI, not a startup crash).
        if (immichMachineLearning is not null)
        {
            context.EnvironmentVariables["IMMICH_MACHINE_LEARNING_URL"] = "http://immich-machine-learning:3003";
        }
    })
    .PublishAsDockerComposeService((_, service) =>
    {
        service.Restart = "unless-stopped";

        service.Volumes.Clear();
        service.Volumes.Add(new Aspire.Hosting.Docker.Resources.ServiceNodes.Volume
        {
            Name = "immich-uploads",
            Type = "bind",
            Source = "./data/immich-uploads",
            Target = "/data",
        });

        service.DependsOn["immich-postgres"] = new Aspire.Hosting.Docker.Resources.ComposeNodes.ServiceDependency { Condition = "service_started" };
        service.DependsOn["immich-redis"] = new Aspire.Hosting.Docker.Resources.ComposeNodes.ServiceDependency { Condition = "service_started" };
        if (immichMachineLearning is not null)
        {
            service.DependsOn["immich-machine-learning"] = new Aspire.Hosting.Docker.Resources.ComposeNodes.ServiceDependency { Condition = "service_started" };
        }
    });

// OpenCloud — bundled third-party file-storage backend for the Storage feature (Phase 4 architecture
// roadmap), an unmodified image like Radicale/Immich above. Unlike Immich's single shared broker
// account, each family member gets a real, individual OpenCloud account mirroring their FamilyCloud
// login (see FamilyCloud.Storage's OpenCloudProvisioner) — FamilyCloud.Server only holds one
// admin/service credential (OpenCloudServiceAccount) to provision those accounts and create/share
// "Spaces" on the family's behalf; it never proxies file bytes itself.
//
// A single container is enough here — unlike Immich, OpenCloud needs no separate Postgres/Redis: its
// built-in LDAP-backed identity store (idm) and "decomposed" (filesystem) storage driver both run
// in-process. Verified against a real local instance: OC_URL must be https:// or the identity-provider
// bootstrap fatally refuses to start, so OC_INSECURE=true (accept its own self-signed cert) takes the
// place of the plain-HTTP posture used elsewhere in this project — every HttpClient that talks to this
// container (FamilyCloud.Storage's OpenCloudClient, the Android app) must override certificate
// validation accordingly. PROXY_ENABLE_BASIC_AUTH is off by default in the image but required here,
// since FamilyCloud authenticates WebDAV/Graph calls with plain username+password, not OIDC.
var openCloudAdminPassword = builder.ExecutionContext.IsPublishMode
    ? "${OPENCLOUD_ADMIN_PASSWORD}"
    : builder.Configuration["OpenCloud:AdminPassword"]
        ?? throw new InvalidOperationException(
            "OpenCloud:AdminPassword is not configured. Set it via user-secrets (dev) — this becomes the " +
            "bundled OpenCloud instance's \"admin\" account password on its very first start only; changes " +
            "afterwards are ignored by OpenCloud itself (see its own docs on changing the admin password).");

var openCloud = builder.AddContainer("opencloud", "opencloudeu/opencloud-rolling", "7.4.0")
    .WithEntrypoint("/bin/sh")
    .WithArgs("-c", "opencloud init || true; opencloud server")
    .WithHttpsEndpoint(port: 9200, targetPort: 9200, name: "https")
    .WithEnvironment("OC_INSECURE", "true")
    .WithEnvironment("PROXY_ENABLE_BASIC_AUTH", "true")
    .WithEnvironment("IDM_ADMIN_PASSWORD", openCloudAdminPassword)
    .WithEnvironment("TZ", "Europe/Berlin")
    .WithVolume("familycloud-opencloud-config", "/etc/opencloud")
    .WithVolume("familycloud-opencloud-data", "/var/lib/opencloud")
    .WithEnvironment(context =>
    {
        // OC_URL has to be set to whatever URL clients actually reach this container by — the same
        // aspire-run-vs-compose split as Radicale/Immich's BaseUrl below, just set directly on this
        // resource since (unlike those) OpenCloud bakes its own public URL into its identity-provider
        // config at first boot, not just something FamilyCloud.Server is told separately.
        context.EnvironmentVariables["OC_URL"] = context.ExecutionContext.IsPublishMode
            ? "https://opencloud:9200"
            : "https://localhost:9200";
    })
    .PublishAsDockerComposeService((_, service) =>
    {
        service.Restart = "unless-stopped";

        service.Volumes.Clear();
        service.Volumes.Add(new Aspire.Hosting.Docker.Resources.ServiceNodes.Volume
        {
            Name = "opencloud-config",
            Type = "bind",
            Source = "./data/opencloud-config",
            Target = "/etc/opencloud",
        });
        service.Volumes.Add(new Aspire.Hosting.Docker.Resources.ServiceNodes.Volume
        {
            Name = "opencloud-data",
            Type = "bind",
            Source = "./data/opencloud-data",
            Target = "/var/lib/opencloud",
        });
    });

builder.AddProject<Projects.FamilyCloud_Server>("server")
    .WaitFor(radicale)
    .WaitFor(immichServer)
    // Deliberately no .WaitFor(openCloud): unlike Radicale/Immich, OpenCloud's HTTPS endpoint uses a
    // self-signed certificate (see OC_INSECURE above), which makes Aspire's own auto-attached HTTP
    // health check for WithHttpsEndpoint fail its TLS validation indefinitely — .WaitFor() would then
    // never unblock. Not needed anyway: FamilyCloud.Storage's OpenCloudProvisioner is designed to
    // tolerate OpenCloud not being up yet (retry with backoff), exactly like ImmichProvisioner already
    // does for immich-server, so the server doesn't need to block its own startup on it.
    .WaitFor(familyCloudDb)
    .WithReference(familyCloudDb)
    .PublishAsDockerComposeService((_, service) =>
    {
        service.Restart = "unless-stopped";

        // The generated docker-compose.yaml only `expose`s container ports by default (reachable from
        // other compose services, not the host). The server is the one thing that has to be reachable
        // from outside the compose network — the app talks to it over the LAN — so publish it explicitly.
        // Host port fixed at 5253 to match local dev; container port stays the ${SERVER_PORT} Aspire
        // already threads through HTTP_PORTS, so both sides of the mapping move together if that ever changes.
        service.Ports.Add("5253:${SERVER_PORT}");

        // Bind mount under ./data next to docker-compose.yaml — now just the Data Protection key ring
        // (the database itself lives in the separate postgres-data volume above since the PostgreSQL
        // switch, see the Phase 1 architecture roadmap). Same reasoning as Radicale's above:
        // .WithVolume(...) doesn't support project resources at all, so this is set directly on the
        // compose model regardless.
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
        // base image runs as a non-root user by default — without this, the server can't create the
        // Data Protection key ring under /data. Running as root is an accepted trade for simplicity
        // here, consistent with the no-TLS/internal-network-only posture already in place.
        service.User = "0:0";
    })
    .WithEnvironment(context =>
    {
        if (context.ExecutionContext.IsPublishMode)
        {
            // ConnectionStrings__DefaultConnection itself comes from .WithReference(familyCloudDb)
            // above, not set here. In local dev the rest come from user-secrets (see Program.cs);
            // Docker Compose has no equivalent, so surface them as blank .env placeholders the
            // deployer fills in before the first `docker compose up` — same pattern used for
            // SERVER_IMAGE/SERVER_PORT above.
            context.EnvironmentVariables["DataDirectory"] = "/data";
            context.EnvironmentVariables["Jwt__SigningKey"] = "${JWT_SIGNING_KEY}";
            context.EnvironmentVariables["SeedAdmin__UserName"] = "${SEED_ADMIN_USERNAME}";
            context.EnvironmentVariables["SeedAdmin__Password"] = "${SEED_ADMIN_PASSWORD}";

            // Both containers reach each other by compose service name on the shared network.
            context.EnvironmentVariables["Radicale__BaseUrl"] = "http://radicale:5232/";
            context.EnvironmentVariables["Radicale__HtpasswdPath"] = "/radicale-auth/users";
            context.EnvironmentVariables["Immich__BaseUrl"] = "http://immich-server:2283/";
            context.EnvironmentVariables["OpenCloud__BaseUrl"] = "https://opencloud:9200/";
            // Same ${OPENCLOUD_ADMIN_PASSWORD} placeholder the container above got as IDM_ADMIN_PASSWORD
            // — the server needs to know the same value to authenticate its own Graph API calls as admin.
            context.EnvironmentVariables["OpenCloud__AdminPassword"] = "${OPENCLOUD_ADMIN_PASSWORD}";
        }
        else
        {
            // `aspire run`: the server runs natively on the host (not containerized), so it reaches
            // Radicale via the host port Aspire publishes for it (container DNS names like "radicale"
            // aren't resolvable outside the Docker network), and writes the htpasswd file straight to
            // the same host path bind-mounted into Radicale's /auth above. Same reasoning for Immich
            // and OpenCloud.
            context.EnvironmentVariables["Radicale__BaseUrl"] = "http://localhost:5232/";
            context.EnvironmentVariables["Radicale__HtpasswdPath"] = "../../.data/radicale-auth/users";
            context.EnvironmentVariables["OpenCloud__AdminPassword"] = openCloudAdminPassword;
            context.EnvironmentVariables["Immich__BaseUrl"] = "http://localhost:2283/";
            context.EnvironmentVariables["OpenCloud__BaseUrl"] = "https://localhost:9200/";
        }
    });

builder.Build().Run();
