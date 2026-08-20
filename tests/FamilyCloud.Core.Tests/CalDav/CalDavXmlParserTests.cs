using FamilyCloud.Core.CalDav;

namespace FamilyCloud.Core.Tests.CalDav;

// Fixture XML captured from a real Radicale server during development (see Phase 2 verification).
public class CalDavXmlParserTests
{
    private static readonly Uri ServerRoot = new("http://localhost:5232/");

    private const string CurrentUserPrincipalResponse =
        """
        <?xml version='1.0' encoding='utf-8'?>
        <multistatus xmlns="DAV:"><response><href>/</href><propstat><prop><current-user-principal><href>/testuser/</href></current-user-principal></prop><status>HTTP/1.1 200 OK</status></propstat></response></multistatus>
        """;

    private const string CalendarHomeSetResponse =
        """
        <?xml version='1.0' encoding='utf-8'?>
        <multistatus xmlns="DAV:" xmlns:C="urn:ietf:params:xml:ns:caldav"><response><href>/testuser/</href><propstat><prop><C:calendar-home-set><href>/testuser/</href></C:calendar-home-set></prop><status>HTTP/1.1 200 OK</status></propstat></response></multistatus>
        """;

    private const string ListCalendarsResponse =
        """
        <?xml version='1.0' encoding='utf-8'?>
        <multistatus xmlns="DAV:" xmlns:C="urn:ietf:params:xml:ns:caldav" xmlns:CS="http://calendarserver.org/ns/"><response><href>/testuser/</href><propstat><prop><resourcetype><principal /><collection /></resourcetype></prop><status>HTTP/1.1 200 OK</status></propstat><propstat><prop><displayname /><C:supported-calendar-component-set /><CS:getctag /></prop><status>HTTP/1.1 404 Not Found</status></propstat></response><response><href>/testuser/household/</href><propstat><prop><resourcetype><C:calendar /><collection /></resourcetype><displayname>Household</displayname><C:supported-calendar-component-set><C:comp name="VEVENT" /></C:supported-calendar-component-set><CS:getctag>"cedf55a62b3b05ef0099b47abd16f1bf950e444cb14717cdf98702612c077392"</CS:getctag></prop><status>HTTP/1.1 200 OK</status></propstat></response></multistatus>
        """;

    private const string CalendarQueryResponse =
        """
        <?xml version='1.0' encoding='utf-8'?>
        <multistatus xmlns="DAV:" xmlns:C="urn:ietf:params:xml:ns:caldav"><response><href>/testuser/household/test-event-1.ics</href><propstat><prop><getetag>"e469d7b1ff4a50ab659807549d1a5f111d7b64688351be6939149d47e71fab2e"</getetag><C:calendar-data>BEGIN:VCALENDAR
        VERSION:2.0
        PRODID:-//FamilyCloud//Test//EN
        BEGIN:VEVENT
        UID:test-event-1@familycloud
        DTSTART:20260825T090000Z
        DTEND:20260825T100000Z
        DTSTAMP:20260819T120000Z
        SUMMARY:Testtermin
        END:VEVENT
        END:VCALENDAR
        </C:calendar-data></prop><status>HTTP/1.1 200 OK</status></propstat></response></multistatus>
        """;

    [Fact]
    public void ParseCurrentUserPrincipal_resolves_href_against_base_uri()
    {
        var result = CalDavXmlParser.ParseCurrentUserPrincipal(CurrentUserPrincipalResponse, ServerRoot);

        Assert.Equal(new Uri("http://localhost:5232/testuser/"), result);
    }

    [Fact]
    public void ParseCalendarHomeSet_resolves_href_against_base_uri()
    {
        var result = CalDavXmlParser.ParseCalendarHomeSet(CalendarHomeSetResponse, new Uri("http://localhost:5232/testuser/"));

        Assert.Equal(new Uri("http://localhost:5232/testuser/"), result);
    }

    [Fact]
    public void ParseCalendarList_skips_non_calendar_collections_and_404_propstats()
    {
        var result = CalDavXmlParser.ParseCalendarList(ListCalendarsResponse, new Uri("http://localhost:5232/testuser/"));

        var calendar = Assert.Single(result);
        Assert.Equal(new Uri("http://localhost:5232/testuser/household/"), calendar.Href);
        Assert.Equal("Household", calendar.DisplayName);
        Assert.Equal("\"cedf55a62b3b05ef0099b47abd16f1bf950e444cb14717cdf98702612c077392\"", calendar.CTag);
    }

    [Fact]
    public void ParseEventResources_extracts_href_etag_and_ics_content()
    {
        var result = CalDavXmlParser.ParseEventResources(CalendarQueryResponse, new Uri("http://localhost:5232/testuser/household/"));

        var resource = Assert.Single(result);
        Assert.Equal(new Uri("http://localhost:5232/testuser/household/test-event-1.ics"), resource.Href);
        Assert.Equal("\"e469d7b1ff4a50ab659807549d1a5f111d7b64688351be6939149d47e71fab2e\"", resource.ETag);
        Assert.Contains("UID:test-event-1@familycloud", resource.IcsContent);
        Assert.Contains("SUMMARY:Testtermin", resource.IcsContent);
    }
}
