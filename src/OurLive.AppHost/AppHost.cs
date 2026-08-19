var builder = DistributedApplication.CreateBuilder(args);

// Bundled "internal" CalDAV server — OurLive.Server connects to it like any other generic CalDAV
// account (same ICalDavClient, no special-casing). Auth stays disabled (auth: none) here: this
// container is never port-published beyond local dev / the compose-internal network, consistent
// with the no-TLS/internal-network-only posture already accepted for the whole system. Revisit if
// Radicale is ever exposed beyond that trust boundary.
var radicale = builder.AddContainer("radicale", "tomsquest/docker-radicale")
    .WithHttpEndpoint(port: 5232, targetPort: 5232, name: "http")
    .WithVolume("ourlive-radicale-data", "/data")
    .WithEnvironment("TZ", "Europe/Berlin");

builder.AddProject<Projects.OurLive_Server>("server")
    .WaitFor(radicale);

builder.Build().Run();
