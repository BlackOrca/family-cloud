namespace FamilyCloud.UI;

/// <summary>One "project" Space from OpenCloud's <c>GET graph/v1.0/me/drives</c> — a FamilyCloud storage
/// root. Deliberately not a FamilyCloud.Contracts DTO: unlike those, this never comes from
/// FamilyCloud.Server, only from OpenCloud's own Graph API (see OpenCloudClient in FamilyCloud.App).</summary>
public sealed record StorageDriveInfo(string Id, string Name);

/// <summary>One file or folder entry parsed from an OpenCloud WebDAV PROPFIND response. <see cref="Path"/>
/// is relative to the drive root (not the queried folder), so it can be handed straight back into another
/// OpenCloudClient call to navigate/download/delete/rename it.</summary>
public sealed record StorageEntry(string Name, string Path, bool IsFolder, long Size, DateTimeOffset? LastModifiedUtc);
