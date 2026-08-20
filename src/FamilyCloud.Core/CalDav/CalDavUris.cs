using FamilyCloud.Core.Domain;

namespace FamilyCloud.Core.CalDav;

public static class CalDavUris
{
    /// <summary>Resolves a calendar's CalDAV resource URI against its account's base URL.</summary>
    public static Uri CalendarUri(CalendarAccount account, Calendar calendar) =>
        new(new Uri(account.BaseUrl), calendar.CalDavHref);
}
