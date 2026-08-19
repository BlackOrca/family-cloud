namespace OurLive.Core.CalDav.Models;

/// <summary>One VEVENT resource as returned by a REPORT calendar-query.</summary>
public sealed record CalDavEventResource(Uri Href, string ETag, string IcsContent);
