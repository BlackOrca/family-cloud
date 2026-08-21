using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using FamilyCloud.Contracts.Storage;
using FamilyCloud.Contracts.Sync;
using FamilyCloud.Core.Auth;
using FamilyCloud.Core.Data;
using FamilyCloud.Core.Sync;
using FamilyCloud.Storage.Domain;
using FamilyCloud.Storage.OpenCloud;

namespace FamilyCloud.Storage.Api;

public static class StorageEndpoints
{
    public static IEndpointRouteBuilder MapStorageEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/storage").RequireAuthorization("MobileApi");

        // FamilyCloud.App talks to OpenCloud's own WebDAV/Graph API directly for everything except root
        // creation/sharing below — it needs to know where that lives, since it otherwise only knows
        // FamilyCloud.Server's own base URL.
        group.MapGet("/config", (IConfiguration configuration) =>
        {
            var baseUrl = configuration["OpenCloud:BaseUrl"] ?? "https://opencloud:9200/";
            return Results.Ok(new StorageConfigDto(WebDavBaseUrl: baseUrl, GraphBaseUrl: baseUrl));
        });

        // Space creation needs OpenCloud's admin account (a regular user gets a 403 "insufficient
        // permissions to create a space" — verified against a real instance), so this can't be done
        // directly from the app the way browsing/upload/download are.
        group.MapPost("/roots", async (
            CreateStorageRootRequest request, HttpContext http, DbContext db, IOpenCloudClient openCloud, SyncEventPublisher syncEvents) =>
        {
            var userId = http.User.GetUserId();
            var account = await db.Set<OpenCloudAccount>().FirstOrDefaultAsync(a => a.UserId == userId);
            if (account is null)
            {
                return Results.Problem(
                    "No OpenCloud account has been provisioned for this user yet — change your password once to trigger provisioning.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var driveId = await openCloud.CreateDriveAsync(request.Name);
            // The creator is the only member who can see a new root until they explicitly share it —
            // same "auto-grant on creation" reasoning as PhotosEndpoints' album creation.
            await openCloud.InviteAsync(driveId, account.OpenCloudUserId, StorageRole.Manager);

            syncEvents.Publish(SyncResourceType.Files, driveId);
            await db.SaveChangesAsync();

            return Results.Created($"/api/storage/roots/{driveId}", new StorageRootDto(driveId, request.Name));
        });

        // Lists everyone a root is currently shared with, resolved from OpenCloud's own drive permissions
        // back to FamilyCloud users via OpenCloudAccount — mirrors Photos'/Lists' GET .../share, even
        // though Storage keeps no local permission table of its own (see the interface's doc-comment on
        // IOpenCloudClient). Gated the same way as the write/revoke endpoints below.
        group.MapGet("/roots/{driveId}/share", async (
            string driveId, HttpContext http, DbContext db, IOpenCloudClient openCloud) =>
        {
            var userId = http.User.GetUserId();
            var callerAccount = await db.Set<OpenCloudAccount>().FirstOrDefaultAsync(a => a.UserId == userId);
            if (callerAccount is null || !await openCloud.HasManagerAccessAsync(driveId, callerAccount.OpenCloudUserId))
            {
                return Results.Forbid(authenticationSchemes: [JwtBearerDefaults.AuthenticationScheme]);
            }

            var permissions = await openCloud.ListPermissionsAsync(driveId);
            var openCloudUserIds = permissions.Select(p => p.OpenCloudUserId).ToList();
            var accounts = await (
                from a in db.Set<OpenCloudAccount>()
                join u in db.Set<AppUser>() on a.UserId equals u.Id
                where openCloudUserIds.Contains(a.OpenCloudUserId)
                select new { a.OpenCloudUserId, u.Id, u.DisplayName }
            ).ToListAsync();

            var shares = permissions
                .Join(accounts, p => p.OpenCloudUserId, a => a.OpenCloudUserId,
                    (p, a) => new StorageRootShareDto(a.Id, a.DisplayName, p.CanWrite))
                .ToList();
            return Results.Ok(shares);
        });

        // Sharing: grants/updates another family member's access. Gated on the caller already holding
        // Manager access to this root — every call here authenticates to OpenCloud as the admin service
        // account, never as the caller, so OpenCloud's own authorization can't enforce this the way it
        // would for a request made directly with the caller's own credentials; HasManagerAccessAsync is
        // the stand-in (same role Photos/Lists' CanWrite permission row plays for their sharing).
        // The target must belong to the same family — this never lets anyone reach across households.
        group.MapPut("/roots/{driveId}/share", async (
            string driveId, ShareStorageRootRequest request, HttpContext http, DbContext db,
            IOpenCloudClient openCloud, IFamilyMembershipChecker membershipChecker) =>
        {
            var userId = http.User.GetUserId();
            var callerAccount = await db.Set<OpenCloudAccount>().FirstOrDefaultAsync(a => a.UserId == userId);
            if (callerAccount is null || !await openCloud.HasManagerAccessAsync(driveId, callerAccount.OpenCloudUserId))
            {
                return Results.Forbid(authenticationSchemes: [JwtBearerDefaults.AuthenticationScheme]);
            }

            var familyId = http.User.GetFamilyId();
            var targetIsFamilyMember = await membershipChecker.IsMemberAsync(familyId, request.UserId);
            if (!targetIsFamilyMember)
            {
                return Results.Problem("Target user is not a member of this family.", statusCode: StatusCodes.Status400BadRequest);
            }

            var targetAccount = await db.Set<OpenCloudAccount>().FirstOrDefaultAsync(a => a.UserId == request.UserId);
            if (targetAccount is null)
            {
                return Results.Problem(
                    "The target user has no OpenCloud account yet — they need to change their password once first.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var role = request.CanWrite ? StorageRole.Editor : StorageRole.Viewer;
            var existingPermissionId = await openCloud.FindPermissionIdAsync(driveId, targetAccount.OpenCloudUserId);
            if (existingPermissionId is null)
            {
                await openCloud.InviteAsync(driveId, targetAccount.OpenCloudUserId, role);
            }
            else
            {
                await openCloud.UpdateRoleAsync(driveId, existingPermissionId, role);
            }

            return Results.NoContent();
        });

        group.MapDelete("/roots/{driveId}/share/{userId:guid}", async (
            string driveId, Guid userId, HttpContext http, DbContext db, IOpenCloudClient openCloud) =>
        {
            var callerId = http.User.GetUserId();
            var callerAccount = await db.Set<OpenCloudAccount>().FirstOrDefaultAsync(a => a.UserId == callerId);
            if (callerAccount is null || !await openCloud.HasManagerAccessAsync(driveId, callerAccount.OpenCloudUserId))
            {
                return Results.Forbid(authenticationSchemes: [JwtBearerDefaults.AuthenticationScheme]);
            }

            var targetAccount = await db.Set<OpenCloudAccount>().FirstOrDefaultAsync(a => a.UserId == userId);
            if (targetAccount is null)
            {
                return Results.NotFound();
            }

            var permissionId = await openCloud.FindPermissionIdAsync(driveId, targetAccount.OpenCloudUserId);
            if (permissionId is null)
            {
                return Results.NotFound();
            }

            await openCloud.RevokeAsync(driveId, permissionId);
            return Results.NoContent();
        });

        return endpoints;
    }
}
