using System.Xml.Linq;
using OurLive.Core.CalDav;

namespace OurLive.Core.Tests.CalDav;

public class CalDavXmlRequestsTests
{
    private static readonly XNamespace Dav = "DAV:";
    private static readonly XNamespace Cal = "urn:ietf:params:xml:ns:caldav";
    private static readonly XNamespace Cs = "http://calendarserver.org/ns/";

    [Fact]
    public void CurrentUserPrincipal_requests_the_DAV_property()
    {
        var xml = CalDavXmlRequests.CurrentUserPrincipal();

        var doc = XDocument.Parse(xml);
        Assert.Equal(Dav + "propfind", doc.Root!.Name);
        Assert.NotNull(doc.Root.Element(Dav + "prop")?.Element(Dav + "current-user-principal"));
    }

    [Fact]
    public void CalendarHomeSet_requests_the_CalDAV_property()
    {
        var xml = CalDavXmlRequests.CalendarHomeSet();

        var doc = XDocument.Parse(xml);
        Assert.NotNull(doc.Root!.Element(Dav + "prop")?.Element(Cal + "calendar-home-set"));
    }

    [Fact]
    public void ListCalendars_requests_resourcetype_displayname_ctag_and_color()
    {
        var xml = CalDavXmlRequests.ListCalendars();

        var prop = XDocument.Parse(xml).Root!.Element(Dav + "prop")!;
        Assert.NotNull(prop.Element(Dav + "resourcetype"));
        Assert.NotNull(prop.Element(Dav + "displayname"));
        Assert.NotNull(prop.Element(Cal + "supported-calendar-component-set"));
        Assert.NotNull(prop.Element(Cs + "getctag"));
        Assert.NotNull(prop.Element(Cs + "calendar-color"));
    }

    [Fact]
    public void CalendarQuery_filters_VEVENTs_by_time_range_in_CalDAV_UTC_format()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero);

        var xml = CalDavXmlRequests.CalendarQuery(start, end);

        var doc = XDocument.Parse(xml);
        Assert.Equal(Cal + "calendar-query", doc.Root!.Name);

        var compFilter = doc.Root
            .Element(Cal + "filter")!
            .Element(Cal + "comp-filter")!; // VCALENDAR
        Assert.Equal("VCALENDAR", compFilter.Attribute("name")!.Value);

        var eventFilter = compFilter.Element(Cal + "comp-filter")!; // VEVENT
        Assert.Equal("VEVENT", eventFilter.Attribute("name")!.Value);

        var timeRange = eventFilter.Element(Cal + "time-range")!;
        Assert.Equal("20260101T000000Z", timeRange.Attribute("start")!.Value);
        Assert.Equal("20261231T000000Z", timeRange.Attribute("end")!.Value);

        var reqProps = doc.Root.Element(Dav + "prop")!;
        Assert.NotNull(reqProps.Element(Dav + "getetag"));
        Assert.NotNull(reqProps.Element(Cal + "calendar-data"));
    }

    [Fact]
    public void MkCalendar_sets_displayname_component_set_and_color()
    {
        var xml = CalDavXmlRequests.MkCalendar("Kinder", "#4287f5");

        var doc = XDocument.Parse(xml);
        Assert.Equal(Cal + "mkcalendar", doc.Root!.Name);

        var prop = doc.Root.Element(Dav + "set")!.Element(Dav + "prop")!;
        Assert.Equal("Kinder", prop.Element(Dav + "displayname")!.Value);
        Assert.Equal("#4287f5", prop.Element(Cs + "calendar-color")!.Value);

        var comp = prop.Element(Cal + "supported-calendar-component-set")!.Element(Cal + "comp")!;
        Assert.Equal("VEVENT", comp.Attribute("name")!.Value);
    }

    [Fact]
    public void MkCalendar_omits_color_when_not_given()
    {
        var xml = CalDavXmlRequests.MkCalendar("Kinder", null);

        var prop = XDocument.Parse(xml).Root!.Element(Dav + "set")!.Element(Dav + "prop")!;
        Assert.Null(prop.Element(Cs + "calendar-color"));
    }

    [Fact]
    public void UpdateCalendarProps_sets_displayname_and_color()
    {
        var xml = CalDavXmlRequests.UpdateCalendarProps("Geburtstage", "#00ff00");

        var doc = XDocument.Parse(xml);
        Assert.Equal(Dav + "propertyupdate", doc.Root!.Name);

        var prop = doc.Root.Element(Dav + "set")!.Element(Dav + "prop")!;
        Assert.Equal("Geburtstage", prop.Element(Dav + "displayname")!.Value);
        Assert.Equal("#00ff00", prop.Element(Cs + "calendar-color")!.Value);
    }

    [Fact]
    public void Requests_include_an_XML_declaration()
    {
        // Some CalDAV servers (e.g. iCloud) reject request bodies without one.
        Assert.StartsWith("<?xml", CalDavXmlRequests.CurrentUserPrincipal());
    }
}
