namespace OurLive.Core.CalDav;

/// <summary>Basic-auth credentials for one CalDAV account. Never logged or persisted in plain text.</summary>
public sealed record CalDavCredentials(string Username, string Password);
