using System.Xml.Linq;

namespace OurLive.Core.CalDav;

/// <summary>WebDAV/CalDAV XML namespace and element name constants shared by request building and response parsing.</summary>
internal static class CalDavXml
{
    public static readonly XNamespace Dav = "DAV:";
    public static readonly XNamespace CalDav = "urn:ietf:params:xml:ns:caldav";
    public static readonly XNamespace CalendarServer = "http://calendarserver.org/ns/";

    public static readonly XName Href = Dav + "href";
    public static readonly XName Response = Dav + "response";
    public static readonly XName PropStat = Dav + "propstat";
    public static readonly XName Prop = Dav + "prop";
    public static readonly XName Status = Dav + "status";
    public static readonly XName ResourceType = Dav + "resourcetype";
    public static readonly XName DisplayName = Dav + "displayname";
    public static readonly XName GetETag = Dav + "getetag";
    public static readonly XName CurrentUserPrincipal = Dav + "current-user-principal";

    public static readonly XName CalendarHomeSet = CalDav + "calendar-home-set";
    public static readonly XName CalendarResourceType = CalDav + "calendar";
    public static readonly XName SupportedCalendarComponentSet = CalDav + "supported-calendar-component-set";
    public static readonly XName CalendarData = CalDav + "calendar-data";

    public static readonly XName GetCTag = CalendarServer + "getctag";
    public static readonly XName CalendarColor = CalendarServer + "calendar-color";
}
