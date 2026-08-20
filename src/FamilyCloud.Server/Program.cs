using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MudBlazor.Services;
using FamilyCloud.Calendar;
using FamilyCloud.Calendar.Api;
using FamilyCloud.Calendar.Domain;
using FamilyCloud.Calendar.Security;
using FamilyCloud.Core.Auth;
using FamilyCloud.Core.Data;
using FamilyCloud.Core.Sync;
using FamilyCloud.Family;
using FamilyCloud.Family.Api;
using FamilyCloud.Family.Domain;
using FamilyCloud.Lists;
using FamilyCloud.Lists.Api;
using FamilyCloud.Server.Api;
using FamilyCloud.Server.Components;
using FamilyCloud.Server.Components.Account;
using FamilyCloud.Server.Data;
using FamilyCloud.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddSingleton<AppTitleNotifier>();

var jwtSigningKey = builder.Configuration["Jwt:SigningKey"]
    ?? throw new InvalidOperationException(
        "Jwt:SigningKey is not configured. Set it via user-secrets (dev) or the Jwt__SigningKey environment variable (Docker).");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "FamilyCloud";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "FamilyCloud";

var authenticationBuilder = builder.Services.AddAuthentication(options =>
{
    // Cookie stays the default scheme so the admin UI's [Authorize] pages keep working
    // unchanged; /api/* endpoints opt into the "MobileApi" (JWT) scheme explicitly below.
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
});
authenticationBuilder.AddIdentityCookies();
authenticationBuilder.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    // Without this, the handler silently remaps short claim types (e.g. "sub") to legacy long-form
    // XML-schema claim URIs, which would break GetUserId()'s literal JwtRegisteredClaimNames.Sub lookup.
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
    };
});

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("MobileApi", policy => policy
        .RequireAuthenticatedUser()
        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme))
    .AddPolicy("FamilyAdmin", policy => policy
        .RequireAuthenticatedUser()
        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
        .RequireClaim(FamilyClaimTypes.FamilyRole, nameof(FamilyRole.Admin)));

// Where FamilyCloud's on-disk state lives that isn't the database itself: the Data Protection key
// ring and the Radicale htpasswd file. Kept independent of the DB connection string (unlike before)
// since that may now point at a Postgres server rather than a local SQLite file.
var dataDirectory = Path.GetFullPath(
    builder.Configuration["DataDirectory"] ?? Path.Combine(builder.Environment.ContentRootPath, "..", "..", ".data"));
Directory.CreateDirectory(dataDirectory);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
// PostgreSQL in production; SQLite only as an explicit test-mode fallback (see
// FamilyCloudWebApplicationFactory) so `dotnet test` doesn't need a live Postgres server.
var useSqlite = string.Equals(builder.Configuration["Database:Provider"], "Sqlite", StringComparison.OrdinalIgnoreCase);
builder.Services.AddDbContext<FamilyCloudDbContext>(options =>
{
    if (useSqlite)
    {
        options.UseSqlite(connectionString);
    }
    else
    {
        options.UseNpgsql(connectionString);
    }
});
// Feature-project-internal code (Calendar's sync services and endpoint handlers, etc.) can't
// reference this concrete, composed context without a circular project reference, so it depends on
// the abstract DbContext instead — resolved here to the same scoped instance. See the Phase 1
// architecture roadmap for the full reasoning.
builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<FamilyCloudDbContext>());
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Generic, cross-feature — every write path (Calendar's, Settings', and future Todos'/Photos') uses
// this the same way to record a SyncEvent atomically alongside its own SaveChangesAsync.
builder.Services.AddScoped<SyncEventPublisher>();

builder.Services.AddDataProtection()
    .SetApplicationName("FamilyCloud")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDirectory, "keys")));

// The htpasswd file the bundled Radicale container's [auth] section is configured to read (see the
// repo-root radicale/config file and its mount in AppHost.cs). Defaults to alongside the data
// directory for plain `dotnet run` without Aspire; Radicale__HtpasswdPath overrides it in both the
// `aspire run` and docker-compose paths, pointing at the mount they actually share with Radicale.
var radicaleHtpasswdPath = builder.Configuration["Radicale:HtpasswdPath"]
    ?? Path.Combine(dataDirectory, "radicale-htpasswd", "users");
var radicaleBaseUrl = builder.Configuration["Radicale:BaseUrl"] ?? "http://radicale:5232/";
builder.Services.AddCalendarFeature(radicaleHtpasswdPath);
builder.Services.AddFamilyFeature();
builder.Services.AddListsFeature();

builder.Services.AddIdentityCore<AppUser>(options =>
    {
        // No email flow: an admin creates accounts directly via the admin UI (Phase 3), so there's
        // no one to confirm a registration.
        options.SignIn.RequireConfirmedAccount = false;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<FamilyCloudDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

var app = builder.Build();

app.MapDefaultEndpoints();

// Apply pending migrations and seed the initial admin account + its family (if none exists yet) on
// every startup — cheap at household scale and means a fresh container just works.
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FamilyCloudDbContext>();
    if (useSqlite)
    {
        // Migration files are generated for PostgreSQL (see Data/Migrations); applying them against
        // SQLite would use provider-mismatched column types. Test-mode databases are always fresh and
        // short-lived, so building the schema directly from the current model is simpler and correct.
        await db.Database.EnsureCreatedAsync();
    }
    else
    {
        await db.Database.MigrateAsync();
    }

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    if (!await userManager.Users.AnyAsync())
    {
        var seedUserName = app.Configuration["SeedAdmin:UserName"];
        var seedPassword = app.Configuration["SeedAdmin:Password"];
        if (!string.IsNullOrWhiteSpace(seedUserName) && !string.IsNullOrWhiteSpace(seedPassword))
        {
            var admin = new AppUser
            {
                UserName = seedUserName,
                DisplayName = "Admin",
                CreatedUtc = DateTimeOffset.UtcNow,
            };
            var result = await userManager.CreateAsync(admin, seedPassword);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to seed admin user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
            app.Logger.LogInformation("Seeded initial admin user {UserName}.", seedUserName);

            // Every deployment starts with exactly one family; the seed admin is its first Admin
            // member. Multi-family operation isn't built yet (see the Phase 1 architecture roadmap)
            // but the schema already supports it.
            var family = new FamilyCloud.Family.Domain.Family
            {
                Id = Guid.NewGuid(),
                Name = app.Configuration["SeedAdmin:FamilyName"] ?? "Familie",
                CreatedUtc = DateTimeOffset.UtcNow,
            };
            db.Families.Add(family);
            db.FamilyMembers.Add(new FamilyMember
            {
                Id = Guid.NewGuid(),
                FamilyId = family.Id,
                UserId = admin.Id,
                Role = FamilyRole.Admin,
                JoinedUtc = DateTimeOffset.UtcNow,
            });

            // Mirror the seed admin's credentials into the bundled Radicale instance so server<->Radicale
            // communication is actually authenticated (it ships with auth disabled otherwise), and record
            // a matching, system-managed CalendarAccount so the admin doesn't have to add it by hand.
            var radicaleProvisioner = scope.ServiceProvider.GetRequiredService<IRadicaleCredentialProvisioner>();
            await radicaleProvisioner.ProvisionAsync(seedUserName, seedPassword);

            var passwordProtector = scope.ServiceProvider.GetRequiredService<ICalDavPasswordProtector>();
            db.CalendarAccounts.Add(new CalendarAccount
            {
                Id = Guid.NewGuid(),
                DisplayName = "Radicale (integriert)",
                BaseUrl = radicaleBaseUrl,
                Username = seedUserName,
                EncryptedAppPassword = passwordProtector.Encrypt(seedPassword),
                IsManaged = true,
                CreatedUtc = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
            app.Logger.LogInformation("Provisioned managed Radicale account and family for {UserName}.", seedUserName);
        }
        else
        {
            app.Logger.LogWarning(
                "No users exist and SeedAdmin:UserName/SeedAdmin:Password are not configured — " +
                "no admin account was created. Set them via user-secrets (dev) or environment variables (Docker).");
        }
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
// No HTTPS/HSTS: the server is only ever reached over the internal network (deliberate
// simplification, see plan point 8) — revisit if this is ever exposed beyond it.
// Only for the Blazor admin UI — without this exclusion it also rewrites /api/* error responses
// (e.g. a plain 403 from Results.Forbid()) into the admin shell's rendered "not found" HTML page.
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/api"),
    branch => branch.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true));

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.MapAuthEndpoints();
app.MapAccountEndpoints();
app.MapFamilyEndpoints();
app.MapCalendarEndpoints();
app.MapListsEndpoints();
app.MapSettingsEndpoints();
app.MapSyncEndpoints();

app.Run();

// Exposes the top-level Program for WebApplicationFactory<Program> in FamilyCloud.Server.Tests.
public partial class Program;
