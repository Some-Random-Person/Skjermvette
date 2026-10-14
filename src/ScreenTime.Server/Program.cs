using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ScreenTime.Server.Auth;
using ScreenTime.Server.Data;
using ScreenTime.Server.Data.Entities;
using ScreenTime.Server.Endpoints;
using ScreenTime.Server.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Missing configuration: ConnectionStrings__DefaultConnection.");
var provider = builder.Configuration["Database:Provider"]
    ?? throw new InvalidOperationException("Missing configuration: Database__Provider (Sqlite or Postgres).");

builder.Services.AddDbContext<AppDbContext>(options => _ = provider switch
{
    "Sqlite" => options.UseSqlite(connectionString),
    "Postgres" => options.UseNpgsql(connectionString),
    _ => throw new InvalidOperationException($"Unknown Database__Provider \"{provider}\". Use Sqlite or Postgres."),
});

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddCookie(IdentityConstants.ApplicationScheme, options =>
    {
        options.Cookie.Name = "screentime.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        // This is an API: answer with status codes instead of redirecting to a login page.
        options.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
        options.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
    })
    .AddScheme<AuthenticationSchemeOptions, DeviceTokenHandler>(DeviceTokenHandler.SchemeName, null);

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AppClaims.ParentPolicy, p => p
        .AddAuthenticationSchemes(IdentityConstants.ApplicationScheme)
        .RequireAuthenticatedUser()
        .RequireClaim(AppClaims.FamilyId))
    .AddPolicy(AppClaims.DevicePolicy, p => p
        .AddAuthenticationSchemes(DeviceTokenHandler.SchemeName)
        .RequireAuthenticatedUser()
        .RequireClaim(AppClaims.DeviceId));

builder.Services.AddIdentityCore<ParentUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        // Length over composition rules (NIST SP 800-63B), so passphrases work.
        options.Password.RequiredLength = 10;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireDigit = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // Pairing codes are 6 digits, so slow down guessing.
    options.AddPolicy(DeviceEndpoints.PairingRateLimit, ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSignalR().AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(OpenApiSetup.Configure);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ChildLocks>();
builder.Services.AddScoped<BudgetService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    // Migrations are generated for Postgres. The SQLite development and test database is created from the model.
    if (db.Database.IsNpgsql())
        await db.Database.MigrateAsync();
    else
        await db.Database.EnsureCreatedAsync();
}

app.UseExceptionHandler(new ExceptionHandlerOptions
{
    // Malformed requests (e.g. invalid JSON) are the client's fault: 400, not 500.
    StatusCodeSelector = ex => ex is BadHttpRequestException bad ? bad.StatusCode : StatusCodes.Status500InternalServerError,
});
app.UseStatusCodePages();
// The parent dashboard (built from web/ into wwwroot).
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    // Browse and try the API at /docs.
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "ScreenTime API");
        options.RoutePrefix = "docs";
        options.DocumentTitle = "ScreenTime API";
    });
}

app.MapAuthEndpoints();
app.MapAccountEndpoints();
app.MapChildEndpoints();
app.MapAppEndpoints();
app.MapDeviceEndpoints();
app.MapHub<BudgetHub>(BudgetHub.Path);

// Dashboard routes like /children/{id}/today are handled in the browser; unknown API paths stay 404.
app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
