namespace FamilyCloud.Photos.Domain;

/// <summary>
/// A FamilyCloud-owned photo album, backed 1:1 by an album in the bundled Immich instance. FamilyCloud
/// never mirrors Immich's own asset data locally (see <see cref="Immich.IImmichClient"/>) — this row
/// only records the mapping and ownership so <see cref="PhotoAlbumPermission"/> can gate access the
/// same way <c>CalendarPermission</c>/<c>ListPermission</c> do for their resources.
/// </summary>
public class PhotoAlbum
{
    public Guid Id { get; set; }

    public Guid FamilyId { get; set; }

    public required string Name { get; set; }

    /// <summary>The corresponding album's id in Immich, created via <see cref="Immich.IImmichClient.CreateAlbumAsync"/>.</summary>
    public required string ImmichAlbumId { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public List<PhotoAlbumPermission> Permissions { get; set; } = [];
}
