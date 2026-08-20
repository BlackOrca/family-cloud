namespace FamilyCloud.Photos.Domain;

/// <summary>Per-user, per-album grant — same shape as <c>CalendarPermission</c>/<c>ListPermission</c>.</summary>
public class PhotoAlbumPermission
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid PhotoAlbumId { get; set; }

    public PhotoAlbum? PhotoAlbum { get; set; }

    public bool CanWrite { get; set; }

    public DateTimeOffset GrantedUtc { get; set; }
}
