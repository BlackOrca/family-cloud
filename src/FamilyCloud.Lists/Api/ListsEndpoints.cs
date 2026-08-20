using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using FamilyCloud.Contracts.Lists;
using FamilyCloud.Contracts.Sync;
using FamilyCloud.Core.Auth;
using FamilyCloud.Core.Sync;
using FamilyCloud.Lists.Domain;

namespace FamilyCloud.Lists.Api;

public static class ListsEndpoints
{
    public static IEndpointRouteBuilder MapListsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/lists").RequireAuthorization("MobileApi");

        // Only the lists the caller has an explicit ListPermission for — never the family's full set.
        group.MapGet("/", async (HttpContext http, DbContext db) =>
        {
            var userId = http.User.GetUserId();
            var lists = await db.Set<ListPermission>()
                .Where(p => p.UserId == userId)
                .Select(p => new ItemListDto(p.ItemList!.Id, p.ItemList.Name, p.ItemList.Kind.ToString(), p.CanWrite))
                .ToListAsync();
            return Results.Ok(lists);
        });

        group.MapPost("/", async (CreateListRequest request, HttpContext http, DbContext db, SyncEventPublisher syncEvents) =>
        {
            if (!Enum.TryParse<ListKind>(request.Kind, ignoreCase: true, out var kind))
            {
                return Results.Problem($"Unknown list kind '{request.Kind}'.", statusCode: StatusCodes.Status400BadRequest);
            }

            var userId = http.User.GetUserId();
            var familyId = http.User.GetFamilyId();

            var list = new ItemList
            {
                Id = Guid.NewGuid(),
                FamilyId = familyId,
                Kind = kind,
                Name = request.Name,
                CreatedUtc = DateTimeOffset.UtcNow,
            };
            db.Set<ItemList>().Add(list);
            // The creator is the only member who can see a new list until they explicitly share it —
            // see POST /{listId}/share. Auto-granted here, not left for a second request, so creation
            // never leaves a list its own creator can't see.
            db.Set<ListPermission>().Add(new ListPermission
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ItemListId = list.Id,
                CanWrite = true,
                GrantedUtc = DateTimeOffset.UtcNow,
            });

            syncEvents.Publish(SyncResourceType.List, list.Id.ToString());
            await db.SaveChangesAsync();

            return Results.Created($"/api/lists/{list.Id}", new ItemListDto(list.Id, list.Name, list.Kind.ToString(), true));
        });

        group.MapDelete("/{listId:guid}", async (Guid listId, HttpContext http, DbContext db, SyncEventPublisher syncEvents) =>
        {
            var userId = http.User.GetUserId();
            var permission = await db.Set<ListPermission>().FirstOrDefaultAsync(p => p.UserId == userId && p.ItemListId == listId);
            if (permission is null || !permission.CanWrite)
            {
                return Results.Forbid(authenticationSchemes: [JwtBearerDefaults.AuthenticationScheme]);
            }

            var list = await db.Set<ItemList>().FirstOrDefaultAsync(l => l.Id == listId);
            if (list is null)
            {
                return Results.NotFound();
            }

            db.Set<ItemList>().Remove(list);
            syncEvents.Publish(SyncResourceType.List, listId.ToString());
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        group.MapGet("/{listId:guid}/items", async (Guid listId, HttpContext http, DbContext db) =>
        {
            var userId = http.User.GetUserId();
            var hasAccess = await db.Set<ListPermission>().AnyAsync(p => p.UserId == userId && p.ItemListId == listId);
            if (!hasAccess)
            {
                return Results.Forbid(authenticationSchemes: [JwtBearerDefaults.AuthenticationScheme]);
            }

            // Materialized first, then ordered/mapped in memory — EF Core's SQLite provider can't
            // translate an ORDER BY on DateTimeOffset (see CalendarsEndpoints for the same
            // limitation), and ToDto isn't SQL-translatable either.
            var items = (await db.Set<ListItem>().Where(i => i.ItemListId == listId).ToListAsync())
                .OrderBy(i => i.Position).ThenBy(i => i.CreatedUtc)
                .Select(ToDto)
                .ToList();
            return Results.Ok(items);
        });

        group.MapPost("/{listId:guid}/items", async (
            Guid listId, ListItemWriteRequest request, HttpContext http, DbContext db, SyncEventPublisher syncEvents) =>
        {
            var userId = http.User.GetUserId();
            var permission = await db.Set<ListPermission>().FirstOrDefaultAsync(p => p.UserId == userId && p.ItemListId == listId);
            if (permission is null || !permission.CanWrite)
            {
                return Results.Forbid(authenticationSchemes: [JwtBearerDefaults.AuthenticationScheme]);
            }

            var nextPosition = 1 + await db.Set<ListItem>()
                .Where(i => i.ItemListId == listId)
                .Select(i => (int?)i.Position)
                .MaxAsync() ?? 0;

            var item = new ListItem
            {
                Id = Guid.NewGuid(),
                ItemListId = listId,
                Text = request.Text,
                Quantity = request.Quantity,
                IsDone = request.IsDone,
                Position = nextPosition,
                CreatedUtc = DateTimeOffset.UtcNow,
                CompletedUtc = request.IsDone ? DateTimeOffset.UtcNow : null,
            };
            db.Set<ListItem>().Add(item);
            syncEvents.Publish(SyncResourceType.List, listId.ToString());
            await db.SaveChangesAsync();

            return Results.Created($"/api/lists/items/{item.Id}", ToDto(item));
        });

        var itemsGroup = endpoints.MapGroup("/api/lists/items").RequireAuthorization("MobileApi");

        itemsGroup.MapPut("/{itemId:guid}", async (
            Guid itemId, ListItemWriteRequest request, HttpContext http, DbContext db, SyncEventPublisher syncEvents) =>
        {
            var userId = http.User.GetUserId();
            var item = await db.Set<ListItem>().FirstOrDefaultAsync(i => i.Id == itemId);
            if (item is null)
            {
                return Results.NotFound();
            }

            var permission = await db.Set<ListPermission>().FirstOrDefaultAsync(p => p.UserId == userId && p.ItemListId == item.ItemListId);
            if (permission is null || !permission.CanWrite)
            {
                return Results.Forbid(authenticationSchemes: [JwtBearerDefaults.AuthenticationScheme]);
            }

            item.Text = request.Text;
            item.Quantity = request.Quantity;
            if (item.IsDone != request.IsDone)
            {
                item.IsDone = request.IsDone;
                item.CompletedUtc = request.IsDone ? DateTimeOffset.UtcNow : null;
            }

            syncEvents.Publish(SyncResourceType.List, item.ItemListId.ToString());
            await db.SaveChangesAsync();
            return Results.Ok(ToDto(item));
        });

        itemsGroup.MapDelete("/{itemId:guid}", async (Guid itemId, HttpContext http, DbContext db, SyncEventPublisher syncEvents) =>
        {
            var userId = http.User.GetUserId();
            var item = await db.Set<ListItem>().FirstOrDefaultAsync(i => i.Id == itemId);
            if (item is null)
            {
                return Results.NotFound();
            }

            var permission = await db.Set<ListPermission>().FirstOrDefaultAsync(p => p.UserId == userId && p.ItemListId == item.ItemListId);
            if (permission is null || !permission.CanWrite)
            {
                return Results.Forbid(authenticationSchemes: [JwtBearerDefaults.AuthenticationScheme]);
            }

            db.Set<ListItem>().Remove(item);
            syncEvents.Publish(SyncResourceType.List, item.ItemListId.ToString());
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // Sharing: grants/updates another family member's access. Gated on the caller already having
        // write access (list "ownership" is just "has a CanWrite permission row"), and the target must
        // belong to the same family — this never lets anyone reach across households.
        group.MapPut("/{listId:guid}/share", async (
            Guid listId, ShareListRequest request, HttpContext http, DbContext db, IFamilyMembershipChecker membershipChecker) =>
        {
            var userId = http.User.GetUserId();
            var callerPermission = await db.Set<ListPermission>().FirstOrDefaultAsync(p => p.UserId == userId && p.ItemListId == listId);
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

            var existing = await db.Set<ListPermission>().FirstOrDefaultAsync(p => p.UserId == request.UserId && p.ItemListId == listId);
            if (existing is null)
            {
                db.Set<ListPermission>().Add(new ListPermission
                {
                    Id = Guid.NewGuid(),
                    UserId = request.UserId,
                    ItemListId = listId,
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

        group.MapDelete("/{listId:guid}/share/{userId:guid}", async (Guid listId, Guid userId, HttpContext http, DbContext db) =>
        {
            var callerId = http.User.GetUserId();
            var callerPermission = await db.Set<ListPermission>().FirstOrDefaultAsync(p => p.UserId == callerId && p.ItemListId == listId);
            if (callerPermission is null || !callerPermission.CanWrite)
            {
                return Results.Forbid(authenticationSchemes: [JwtBearerDefaults.AuthenticationScheme]);
            }

            var existing = await db.Set<ListPermission>().FirstOrDefaultAsync(p => p.UserId == userId && p.ItemListId == listId);
            if (existing is null)
            {
                return Results.NotFound();
            }

            db.Set<ListPermission>().Remove(existing);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return endpoints;
    }

    private static ListItemDto ToDto(ListItem i) => new(i.Id, i.ItemListId, i.Text, i.Quantity, i.IsDone, i.Position);
}
