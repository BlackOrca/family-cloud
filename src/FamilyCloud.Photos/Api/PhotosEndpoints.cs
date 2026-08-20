using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using FamilyCloud.Contracts.Photos;
using FamilyCloud.Contracts.Sync;
using FamilyCloud.Core.Auth;
using FamilyCloud.Core.Data;
using FamilyCloud.Core.Sync;
using FamilyCloud.Photos.Domain;
using FamilyCloud.Photos.Immich;

namespace FamilyCloud.Photos.Api;

public static class PhotosEndpoints
{
    public static IEndpointRouteBuilder MapPhotosEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/photos/albums").RequireAuthorization("MobileApi");

        // Only the albums the caller has an explicit PhotoAlbumPermission for — never the family's full set.
        group.MapGet("/", async (HttpContext http, DbContext db) =>
        {
            var userId = http.User.GetUserId();
            var albums = await db.Set<PhotoAlbumPermission>()
                .Where(p => p.UserId == userId)
                .Select(p => new PhotoAlbumDto(p.PhotoAlbum!.Id, p.PhotoAlbum.Name, p.CanWrite))
                .ToListAsync();
            return Results.Ok(albums);
        });

        group.MapPost("/", async (
            CreatePhotoAlbumRequest request, HttpContext http, DbContext db, IImmichClient immich, SyncEventPublisher syncEvents) =>
        {
            var userId = http.User.GetUserId();
            var familyId = http.User.GetFamilyId();

            var immichAlbumId = await immich.CreateAlbumAsync(request.Name);

            var album = new PhotoAlbum
            {
                Id = Guid.NewGuid(),
                FamilyId = familyId,
                Name = request.Name,
                ImmichAlbumId = immichAlbumId,
                CreatedUtc = DateTimeOffset.UtcNow,
            };
            db.Set<PhotoAlbum>().Add(album);
            // The creator is the only member who can see a new album until they explicitly share it —
            // see PUT /{albumId}/share. Auto-granted here, not left for a second request, so creation
            // never leaves an album its own creator can't see.
            db.Set<PhotoAlbumPermission>().Add(new PhotoAlbumPermission
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PhotoAlbumId = album.Id,
                CanWrite = true,
                GrantedUtc = DateTimeOffset.UtcNow,
            });

            syncEvents.Publish(SyncResourceType.Photo, album.Id.ToString());
            await db.SaveChangesAsync();

            return Results.Created($"/api/photos/albums/{album.Id}", new PhotoAlbumDto(album.Id, album.Name, true));
        });

        group.MapDelete("/{albumId:guid}", async (Guid albumId, HttpContext http, DbContext db, IImmichClient immich, SyncEventPublisher syncEvents) =>
        {
            var userId = http.User.GetUserId();
            var permission = await db.Set<PhotoAlbumPermission>().FirstOrDefaultAsync(p => p.UserId == userId && p.PhotoAlbumId == albumId);
            if (permission is null || !permission.CanWrite)
            {
                return Results.Forbid(authenticationSchemes: [JwtBearerDefaults.AuthenticationScheme]);
            }

            var album = await db.Set<PhotoAlbum>().FirstOrDefaultAsync(a => a.Id == albumId);
            if (album is null)
            {
                return Results.NotFound();
            }

            await immich.DeleteAlbumAsync(album.ImmichAlbumId);

            db.Set<PhotoAlbum>().Remove(album);
            syncEvents.Publish(SyncResourceType.Photo, albumId.ToString());
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        group.MapGet("/{albumId:guid}/assets", async (Guid albumId, HttpContext http, DbContext db, IImmichClient immich) =>
        {
            var userId = http.User.GetUserId();
            var album = await db.Set<PhotoAlbum>().FirstOrDefaultAsync(a => a.Id == albumId);
            if (album is null)
            {
                return Results.NotFound();
            }

            var hasAccess = await db.Set<PhotoAlbumPermission>().AnyAsync(p => p.UserId == userId && p.PhotoAlbumId == albumId);
            if (!hasAccess)
            {
                return Results.Forbid(authenticationSchemes: [JwtBearerDefaults.AuthenticationScheme]);
            }

            var assets = await immich.GetAlbumAssetsAsync(album.ImmichAlbumId);
            var dtos = assets
                .Select(a => new PhotoAssetDto(a.Id, a.OriginalFileName, a.FileCreatedAt, $"/api/photos/albums/{albumId}/assets/{a.Id}/thumbnail"))
                .ToList();
            return Results.Ok(dtos);
        });

        group.MapPost("/{albumId:guid}/assets", async (
            Guid albumId, IFormFile file, HttpContext http, DbContext db, IImmichClient immich, SyncEventPublisher syncEvents) =>
        {
            var userId = http.User.GetUserId();
            var album = await db.Set<PhotoAlbum>().FirstOrDefaultAsync(a => a.Id == albumId);
            if (album is null)
            {
                return Results.NotFound();
            }

            var permission = await db.Set<PhotoAlbumPermission>().FirstOrDefaultAsync(p => p.UserId == userId && p.PhotoAlbumId == albumId);
            if (permission is null || !permission.CanWrite)
            {
                return Results.Forbid(authenticationSchemes: [JwtBearerDefaults.AuthenticationScheme]);
            }

            await using var stream = file.OpenReadStream();
            var assetId = await immich.UploadAssetAsync(album.ImmichAlbumId, file.FileName, stream, file.ContentType);

            syncEvents.Publish(SyncResourceType.Photo, albumId.ToString());
            await db.SaveChangesAsync();

            return Results.Created(
                $"/api/photos/albums/{albumId}/assets/{assetId}",
                new PhotoAssetDto(assetId, file.FileName, DateTimeOffset.UtcNow, $"/api/photos/albums/{albumId}/assets/{assetId}/thumbnail"));
        }).DisableAntiforgery();

        group.MapDelete("/{albumId:guid}/assets/{assetId}", async (
            Guid albumId, string assetId, HttpContext http, DbContext db, IImmichClient immich, SyncEventPublisher syncEvents) =>
        {
            var userId = http.User.GetUserId();
            var permission = await db.Set<PhotoAlbumPermission>().FirstOrDefaultAsync(p => p.UserId == userId && p.PhotoAlbumId == albumId);
            if (permission is null || !permission.CanWrite)
            {
                return Results.Forbid(authenticationSchemes: [JwtBearerDefaults.AuthenticationScheme]);
            }

            await immich.DeleteAssetAsync(assetId);
            syncEvents.Publish(SyncResourceType.Photo, albumId.ToString());
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        group.MapGet("/{albumId:guid}/assets/{assetId}/thumbnail", async (Guid albumId, string assetId, HttpContext http, DbContext db, IImmichClient immich) =>
        {
            var userId = http.User.GetUserId();
            var hasAccess = await db.Set<PhotoAlbumPermission>().AnyAsync(p => p.UserId == userId && p.PhotoAlbumId == albumId);
            if (!hasAccess)
            {
                return Results.Forbid(authenticationSchemes: [JwtBearerDefaults.AuthenticationScheme]);
            }

            var thumbnail = await immich.GetAssetThumbnailAsync(assetId);
            return Results.Stream(thumbnail.Content, thumbnail.ContentType);
        });

        group.MapGet("/{albumId:guid}/share", async (Guid albumId, HttpContext http, DbContext db) =>
        {
            var userId = http.User.GetUserId();
            var hasAccess = await db.Set<PhotoAlbumPermission>().AnyAsync(p => p.UserId == userId && p.PhotoAlbumId == albumId);
            if (!hasAccess)
            {
                return Results.Forbid(authenticationSchemes: [JwtBearerDefaults.AuthenticationScheme]);
            }

            var shares = await (
                from p in db.Set<PhotoAlbumPermission>()
                join u in db.Set<AppUser>() on p.UserId equals u.Id
                where p.PhotoAlbumId == albumId
                select new PhotoAlbumShareDto(u.Id, u.DisplayName, p.CanWrite)
            ).ToListAsync();
            return Results.Ok(shares);
        });

        // Sharing: grants/updates another family member's access. Gated on the caller already having
        // write access (album "ownership" is just "has a CanWrite permission row"), and the target must
        // belong to the same family — this never lets anyone reach across households.
        group.MapPut("/{albumId:guid}/share", async (
            Guid albumId, SharePhotoAlbumRequest request, HttpContext http, DbContext db, IFamilyMembershipChecker membershipChecker) =>
        {
            var userId = http.User.GetUserId();
            var callerPermission = await db.Set<PhotoAlbumPermission>().FirstOrDefaultAsync(p => p.UserId == userId && p.PhotoAlbumId == albumId);
            if (callerPermission is null || !callerPermission.CanWrite)
            {
                return Results.Forbid(authenticationSchemes: [JwtBearerDefaults.AuthenticationScheme]);
            }

            var familyId = http.User.GetFamilyId();
            var targetIsFamilyMember = await membershipChecker.IsMemberAsync(familyId, request.UserId);
            if (!targetIsFamilyMember)
            {
                return Results.Problem("Target user is not a member of this family.", statusCode: StatusCodes.Status400BadRequest);
            }

            var existing = await db.Set<PhotoAlbumPermission>().FirstOrDefaultAsync(p => p.UserId == request.UserId && p.PhotoAlbumId == albumId);
            if (existing is null)
            {
                db.Set<PhotoAlbumPermission>().Add(new PhotoAlbumPermission
                {
                    Id = Guid.NewGuid(),
                    UserId = request.UserId,
                    PhotoAlbumId = albumId,
                    CanWrite = request.CanWrite,
                    GrantedUtc = DateTimeOffset.UtcNow,
                });
            }
            else
            {
                existing.CanWrite = request.CanWrite;
            }

            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        group.MapDelete("/{albumId:guid}/share/{userId:guid}", async (Guid albumId, Guid userId, HttpContext http, DbContext db) =>
        {
            var callerId = http.User.GetUserId();
            var callerPermission = await db.Set<PhotoAlbumPermission>().FirstOrDefaultAsync(p => p.UserId == callerId && p.PhotoAlbumId == albumId);
            if (callerPermission is null || !callerPermission.CanWrite)
            {
                return Results.Forbid(authenticationSchemes: [JwtBearerDefaults.AuthenticationScheme]);
            }

            var existing = await db.Set<PhotoAlbumPermission>().FirstOrDefaultAsync(p => p.UserId == userId && p.PhotoAlbumId == albumId);
            if (existing is null)
            {
                return Results.NotFound();
            }

            db.Set<PhotoAlbumPermission>().Remove(existing);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return endpoints;
    }
}
