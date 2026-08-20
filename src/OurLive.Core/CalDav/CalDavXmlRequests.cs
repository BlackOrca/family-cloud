using System.Globalization;
using System.Xml.Linq;

namespace OurLive.Core.CalDav;

/// <summary>Builds the XML request bodies for the handful of CalDAV/WebDAV operations this client needs.</summary>
internal static class CalDavXmlRequests
{
    public static string CurrentUserPrincipal()
    {
        var d = CalDavXml.Dav;
        return Serialize(new XElement(d + "propfind",
            new XElement(d + "prop",
                new XElement(d + "current-user-principal"))));
    }

    public static string CalendarHomeSet()
    {
        var d = CalDavXml.Dav;
        var c = CalDavXml.CalDav;
        return Serialize(new XElement(d + "propfind",
            new XElement(d + "prop",
                new XElement(c + "calendar-home-set"))));
    }

    public static string ListCalendars()
    {
        var d = CalDavXml.Dav;
        var c = CalDavXml.CalDav;
        var cs = CalDavXml.CalendarServer;
        return Serialize(new XElement(d + "propfind",
            new XElement(d + "prop",
                new XElement(d + "resourcetype"),
                new XElement(d + "displayname"),
                new XElement(c + "supported-calendar-component-set"),
                new XElement(cs + "getctag"),
                new XElement(cs + "calendar-color"))));
    }

    public static string CalendarQuery(DateTimeOffset start, DateTimeOffset end)
    {
        var d = CalDavXml.Dav;
        var c = CalDavXml.CalDav;
        return Serialize(new XElement(c + "calendar-query",
            new XElement(d + "prop",
                new XElement(d + "getetag"),
                new XElement(c + "calendar-data")),
            new XElement(c + "filter",
                new XElement(c + "comp-filter", new XAttribute("name", "VCALENDAR"),
                    new XElement(c + "comp-filter", new XAttribute("name", "VEVENT"),
                        new XElement(c + "time-range",
                            new XAttribute("start", ToCalDavTime(start)),
                            new XAttribute("end", ToCalDavTime(end))))))));
    }

    public static string MkCalendar(string displayName, string? colorHex)
    {
        var d = CalDavXml.Dav;
        var c = CalDavXml.CalDav;
        var cs = CalDavXml.CalendarServer;
        var prop = new XElement(d + "prop",
            new XElement(d + "displayname", displayName),
            new XElement(c + "supported-calendar-component-set",
                new XElement(c + "comp", new XAttribute("name", "VEVENT"))));
        if (colorHex is not null)
        {
            prop.Add(new XElement(cs + "calendar-color", colorHex));
        }

        return Serialize(new XElement(c + "mkcalendar",
            new XElement(d + "set", prop)));
    }

    public static string UpdateCalendarProps(string displayName, string? colorHex)
    {
        var d = CalDavXml.Dav;
        var cs = CalDavXml.CalendarServer;
        var prop = new XElement(d + "prop",
            new XElement(d + "displayname", displayName));
        if (colorHex is not null)
        {
            prop.Add(new XElement(cs + "calendar-color", colorHex));
        }

        return Serialize(new XElement(d + "propertyupdate",
            new XElement(d + "set", prop)));
    }

    private static string ToCalDavTime(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    private static string Serialize(XElement root)
    {
        // XDocument.ToString() omits the XML declaration; some CalDAV servers (e.g. iCloud) require it.
        var declaration = new XDeclaration("1.0", "utf-8", null);
        return declaration + Environment.NewLine + root.ToString(SaveOptions.DisableFormatting);
    }
}
