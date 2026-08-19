using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MudBlazor.Services;
using OurLive.Core.CalDav;
using OurLive.Core.Data;
using OurLive.Core.Security;
using OurLive.Core.Sync;
using OurLive.Server.Api;
using OurLive.Server.Components;
using OurLive.Server.Components.Account;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddHttpClient<ICalDavClient, CalDavClient>();
builder.Services.AddScoped<CalendarSyncService>();
builder.Services.AddScoped<CalendarWriteService>();

var jwtSigningKey = builder.Configuration["Jwt:SigningKey"]
    ?? throw new InvalidOperationException(
        "Jwt:SigningKey is not configured. Set it via user-secrets (dev) or the Jwt__SigningKey environment variable (Docker).");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "OurLive";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "OurLive";

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
        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme));

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// SQLite creates the database file itself but not its parent directory (e.g. the repo-root
// .data/ folder for local dev, or a not-yet-existing bind mount target). The Data Protection key
// ring lives alongside it so both survive on the same volume across container recreations.
var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
var dataDirectory = Path.GetDirectoryName(Path.GetFullPath(dataSource))
    ?? throw new InvalidOperationException($"Could not determine a data directory from connection string data source '{dataSource}'.");
Directory.CreateDirectory(dataDirectory);

builder.Services.AddDbContext<OurLiveDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDataProtection()
    .SetApplicationName("OurLive")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDirectory, "keys")));
builder.Services.AddSingleton<ICalDavPasswordProtector, CalDavPasswordProtector>();

builder.Services.AddIdentityCore<AppUser>(options =>
    {
        // No email flow: an admin creates accounts directly via the admin UI (Phase 3), so there's
        // no one to confirm a registration.
        options.SignIn.RequireConfirmedAccount = false;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<OurLiveDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

var app = builder.Build();

app.MapDefaultEndpoints();

// Apply pending migrations and seed the initial admin account (if none exists yet) on every
// startup — cheap at household scale and means a fresh container just works.
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OurLiveDbContext>();
    await db.Database.MigrateAsync();

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
app.MapCalendarsEndpoints();

app.Run();
