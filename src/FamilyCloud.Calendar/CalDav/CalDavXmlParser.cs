using System.Xml.Linq;
using FamilyCloud.Calendar.CalDav.Models;

namespace FamilyCloud.Calendar.CalDav;

/// <summary>Parses the DAV:multistatus XML bodies returned by PROPFIND/REPORT requests.</summary>
internal static class CalDavXmlParser
{
    public static Uri? ParseCurrentUserPrincipal(string xml, Uri baseUri) =>
        ResolveFirstHref(xml, baseUri, CalDavXml.CurrentUserPrincipal);

    public static Uri? ParseCalendarHomeSet(string xml, Uri baseUri) =>
        ResolveFirstHref(xml, baseUri, CalDavXml.CalendarHomeSet);

    public static IReadOnlyList<CalDavCalendarInfo> ParseCalendarList(string xml, Uri baseUri)
    {
        var results = new List<CalDavCalendarInfo>();

        foreach (var response in XDocument.Parse(xml).Descendants(CalDavXml.Response))
        {
            var hrefValue = response.Element(CalDavXml.Href)?.Value;
            if (string.IsNullOrEmpty(hrefValue))
            {
                continue;
            }

            var prop = MergeSuccessfulProps(response);
            var isCalendar = prop.Element(CalDavXml.ResourceType)?.Element(CalDavXml.CalendarResourceType) is not null;
            if (!isCalendar)
            {
                continue;
            }

            var displayName = prop.Element(CalDavXml.DisplayName)?.Value;
            results.Add(new CalDavCalendarInfo(
                Href: new Uri(baseUri, hrefValue),
                DisplayName: string.IsNullOrEmpty(displayName) ? hrefValue : displayName,
                ColorHex: prop.Element(CalDavXml.CalendarColor)?.Value,
                CTag: prop.Element(CalDavXml.GetCTag)?.Value));
        }

        return results;
    }

    public static IReadOnlyList<CalDavEventResource> ParseEventResources(string xml, Uri baseUri)
    {
        var results = new List<CalDavEventResource>();

        foreach (var response in XDocument.Parse(xml).Descendants(CalDavXml.Response))
        {
            var hrefValue = response.Element(CalDavXml.Href)?.Value;
            if (string.IsNullOrEmpty(hrefValue))
            {
                continue;
            }

            var prop = MergeSuccessfulProps(response);
            var etag = prop.Element(CalDavXml.GetETag)?.Value;
            var icsContent = prop.Element(CalDavXml.CalendarData)?.Value;
            if (string.IsNullOrEmpty(etag) || string.IsNullOrEmpty(icsContent))
            {
                continue;
            }

            results.Add(new CalDavEventResource(new Uri(baseUri, hrefValue), etag, icsContent));
        }

        return results;
    }

    private static Uri? ResolveFirstHref(string xml, Uri baseUri, XName containerElement)
    {
        var hrefValue = XDocument.Parse(xml).Descendants(containerElement)
            .Descendants(CalDavXml.Href)
            .Select(e => e.Value)
            .FirstOrDefault();
        return string.IsNullOrEmpty(hrefValue) ? null : new Uri(baseUri, hrefValue);
    }

    /// <summary>
    /// A DAV:response can carry several DAV:propstat blocks (one per HTTP status found among the
    /// requested properties, e.g. some 200 OK and some 404 Not Found). Only properties reported
    /// under a 200 OK propstat are meaningful, so this merges just those into one synthetic
    /// &lt;prop&gt; element for simple lookup.
    /// </summary>
    private static XElement MergeSuccessfulProps(XElement response)
    {
        var merged = new XElement(CalDavXml.Prop);
        foreach (var propstat in response.Elements(CalDavXml.PropStat))
        {
            var status = propstat.Element(CalDavXml.Status)?.Value ?? "";
            if (!status.Contains(" 200 ", StringComparison.Ordinal) && !status.TrimEnd().EndsWith(" 200", StringComparison.Ordinal))
            {
                continue;
            }

            var prop = propstat.Element(CalDavXml.Prop);
            if (prop is not null)
            {
                merged.Add(prop.Elements());
            }
        }

        return merged;
    }
}
