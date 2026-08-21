namespace FamilyCloud.Contracts.Storage;

/// <summary>Tells FamilyCloud.App where the bundled OpenCloud instance's WebDAV/Graph API live — the
/// app talks to OpenCloud directly for everything except root creation/sharing (see StorageEndpoints),
/// so it needs OpenCloud's own base URL, not just FamilyCloud.Server's.</summary>
public sealed record StorageConfigDto(string WebDavBaseUrl, string GraphBaseUrl);
