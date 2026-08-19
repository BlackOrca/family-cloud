namespace OurLive.Core.CalDav.Models;

/// <summary>One calendar collection as discovered via PROPFIND on the calendar-home-set.</summary>
public sealed record CalDavCalendarInfo(Uri Href, string DisplayName, string? ColorHex, string? CTag);
