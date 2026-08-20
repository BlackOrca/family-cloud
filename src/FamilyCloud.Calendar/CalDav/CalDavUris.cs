using FamilyCloud.Calendar.Domain;

namespace FamilyCloud.Calendar.CalDav;

public static class CalDavUris
{
    // "Calendar" unqualified would resolve to the FamilyCloud.Calendar *namespace* (this project's own
    // root), not the Domain.Calendar *class*, since this file is nested inside that namespace — hence
    // the Domain.-qualified reference below instead of relying on the `using` above for this one type.
    /// <summary>Resolves a calendar's CalDAV resource URI against its account's base URL.</summary>
    public static Uri CalendarUri(CalendarAccount account, Domain.Calendar calendar) =>
        new(new Uri(account.BaseUrl), calendar.CalDavHref);
}
