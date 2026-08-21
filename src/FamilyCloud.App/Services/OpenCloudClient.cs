using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Xml.Linq;
using FamilyCloud.UI;

namespace FamilyCloud.App.Services;

/// <summary>Talks to the bundled OpenCloud instance's WebDAV/Graph API directly — unlike ApiClient, which
/// always goes through FamilyCloud.Server. Authenticates with Basic-Auth using the same username/password
/// as the current FamilyCloud login (OpenCloud accounts mirror FamilyCloud accounts 1:1, see
/// OpenCloudAccount on the server, and AppAuthState/AuthTokenStore for where the password comes from).
/// URL shapes, the PROPFIND XML response format, and header names below were verified against a real
/// running OpenCloud 7.4.0 instance (docker run + curl), not just documentation — see the Phase 4
/// architecture roadmap.</summary>
internal sealed class OpenCloudClient(HttpClient http, ApiClient api, AppAuthState authState)
{
    private static readonly XNamespace Dav = "DAV:";

    private const string PropfindBody =
        """<?xml version="1.0"?><d:propfind xmlns:d="DAV:"><d:prop><d:resourcetype/><d:getcontentlength/><d:getlastmodified/></d:prop></d:propfind>""";

    private string? _webDavBaseUrl;
    private string? _graphBaseUrl;

    private async Task EnsureConfigAsync(CancellationToken ct)
    {
        if (_webDavBaseUrl is not null)
        {
            return;
        }

        var config = await api.GetStorageConfigAsync(ct)
            ?? throw new InvalidOperationException("Storage ist auf dem Server nicht konfiguriert.");
        _webDavBaseUrl = config.WebDavBaseUrl.EndsWith('/') ? config.WebDavBaseUrl : config.WebDavBaseUrl + "/";
        _graphBaseUrl = config.GraphBaseUrl.EndsWith('/') ? config.GraphBaseUrl : config.GraphBaseUrl + "/";
    }

    private void AddAuth(HttpRequestMessage request)
    {
        var bytes = Encoding.UTF8.GetBytes($"{authState.UserName}:{authState.Password}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(bytes));
    }

    private sealed record DriveRootDto(string WebDavUrl);
    private sealed record DriveDto(string Id, string Name, string DriveType);
    private sealed record DrivesResponse(List<DriveDto> Value);

    public async Task<List<StorageDriveInfo>> GetMyDrivesAsync(CancellationToken ct = default)
    {
        await EnsureConfigAsync(ct);
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(_graphBaseUrl!), "graph/v1.0/me/drives"));
        AddAuth(request);
        var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var parsed = await response.Content.ReadFromJsonAsync<DrivesResponse>(ct);

        // Only "project" spaces are FamilyCloud storage roots (POST /api/storage/roots creates exactly
        // these) — excludes the caller's own "personal" home drive and the virtual "shares" aggregate
        // drive, neither of which FamilyCloud's Storage feature manages. Verified against a live instance.
        return parsed?.Value
            .Where(d => d.DriveType == "project")
            .Select(d => new StorageDriveInfo(d.Id, d.Name))
            .ToList() ?? [];
    }

    public async Task<List<StorageEntry>> ListAsync(string driveId, string path, CancellationToken ct = default)
    {
        await EnsureConfigAsync(ct);
        var requestUri = BuildDavUri(driveId, path);

        using var request = new HttpRequestMessage(new HttpMethod("PROPFIND"), requestUri)
        {
            Content = new StringContent(PropfindBody, Encoding.UTF8, "application/xml"),
        };
        request.Headers.Add("Depth", "1");
        AddAuth(request);

        var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var xml = await response.Content.ReadAsStringAsync(ct);
        return ParseMultistatus(xml, driveId, requestUri);
    }

    public async Task CreateFolderAsync(string driveId, string path, CancellationToken ct = default)
    {
        await EnsureConfigAsync(ct);
        using var request = new HttpRequestMessage(new HttpMethod("MKCOL"), BuildDavUri(driveId, path));
        AddAuth(request);
        var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task UploadAsync(string driveId, string path, Stream content, string contentType, CancellationToken ct = default)
    {
        await EnsureConfigAsync(ct);
        using var request = new HttpRequestMessage(HttpMethod.Put, BuildDavUri(driveId, path))
        {
            Content = new StreamContent(content),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        AddAuth(request);
        var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<(byte[] Bytes, string ContentType)> DownloadAsync(string driveId, string path, CancellationToken ct = default)
    {
        await EnsureConfigAsync(ct);
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildDavUri(driveId, path));
        AddAuth(request);
        var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        return (bytes, contentType);
    }

    public async Task DeleteAsync(string driveId, string path, CancellationToken ct = default)
    {
        await EnsureConfigAsync(ct);
        using var request = new HttpRequestMessage(HttpMethod.Delete, BuildDavUri(driveId, path));
        AddAuth(request);
        var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    // dav/spaces/{driveId}/{path} — the driveId itself (e.g. "b8702152-...$9e64e2e8-...") is passed
    // unescaped: its '$' separator is a valid WebDAV path character and gets rejected if percent-encoded
    // (verified against a live instance). Only the caller-supplied path segments get escaped.
    private Uri BuildDavUri(string driveId, string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString);
        var relative = string.Join('/', new[] { "dav", "spaces", driveId }.Concat(segments));
        return new Uri(new Uri(_webDavBaseUrl!), relative);
    }

    private static List<StorageEntry> ParseMultistatus(string xml, string driveId, Uri requestUri)
    {
        var doc = XDocument.Parse(xml);
        var selfPath = Uri.UnescapeDataString(requestUri.AbsolutePath).TrimEnd('/');
        var driveRootPrefix = $"/dav/spaces/{driveId}/";

        var entries = new List<StorageEntry>();
        foreach (var responseEl in doc.Descendants(Dav + "response"))
        {
            var href = responseEl.Element(Dav + "href")?.Value;
            if (href is null)
            {
                continue;
            }

            var decodedPath = Uri.UnescapeDataString(href).TrimEnd('/');
            if (decodedPath == selfPath)
            {
                // The queried folder's own entry — PROPFIND with Depth:1 includes it first alongside its children.
                continue;
            }

            var propstat = responseEl.Elements(Dav + "propstat")
                .FirstOrDefault(p => (p.Element(Dav + "status")?.Value ?? "").Contains("200"));
            if (propstat is null)
            {
                continue;
            }

            var prop = propstat.Element(Dav + "prop");
            var isFolder = prop?.Element(Dav + "resourcetype")?.Element(Dav + "collection") is not null;
            var size = long.TryParse(prop?.Element(Dav + "getcontentlength")?.Value, out var parsedSize) ? parsedSize : 0L;
            var modified = DateTimeOffset.TryParse(prop?.Element(Dav + "getlastmodified")?.Value, out var parsedModified)
                ? parsedModified
                : (DateTimeOffset?)null;

            var relativePath = decodedPath.StartsWith(driveRootPrefix, StringComparison.Ordinal)
                ? decodedPath[driveRootPrefix.Length..]
                : decodedPath.TrimStart('/');
            var name = relativePath.Contains('/') ? relativePath[(relativePath.LastIndexOf('/') + 1)..] : relativePath;

            entries.Add(new StorageEntry(name, relativePath, isFolder, size, modified));
        }

        return entries.OrderByDescending(e => e.IsFolder).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
