namespace OurLive.Core.CalDav;

/// <summary>A CalDAV request failed (non-success status, or a required property was missing from the response).</summary>
public sealed class CalDavException(string message) : Exception(message);
