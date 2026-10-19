using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Skjermvette.Server.Auth;
using Skjermvette.Server.Data;
using Skjermvette.Server.Data.Entities;
using Skjermvette.Server.Email;
using Skjermvette.Server.Endpoints;
using Skjermvette.Server.Services;

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
        options.Cookie.Name = "skjermvette.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        // This is an API: answer with status codes instead of redirecting to a login page.
        options.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
        options.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
        // Re-check the sign-in against the database (security stamp): a changed password or email,
        // or a deleted account, signs other browsers out within ParentSession.RecheckInterval.
        options.Events.OnValidatePrincipal = SecurityStampValidator.ValidatePrincipalAsync;
    })
    // Between the password and the second-step code (ParentSignInManager).
    .AddCookie(IdentityConstants.TwoFactorUserIdScheme, options =>
    {
        options.Cookie.Name = "skjermvette.2fa";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = ParentSession.SecondStepLifespan;
    })
    // "Remember this browser": skips the second step. Forgotten when the security stamp changes.
    .AddCookie(IdentityConstants.TwoFactorRememberMeScheme, options =>
    {
        options.Cookie.Name = "skjermvette.remember";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = ParentSession.RememberBrowserLifespan;
        options.Events.OnValidatePrincipal = SecurityStampValidator.ValidateAsync<ITwoFactorSecurityStampValidator>;
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
        options.Tokens.PasswordResetTokenProvider = PasswordResetTokenProvider.ProviderName;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager<ParentSignInManager>()
    .AddClaimsPrincipalFactory<ParentClaimsFactory>()
    .AddDefaultTokenProviders()
    // Replaces Identity's own email provider (same name), so emailed sign-in codes are single use.
    .AddTokenProvider<EmailCodeTokenProvider>(EmailCodeTokenProvider.ProviderName)
    .AddTokenProvider<PasswordResetTokenProvider>(PasswordResetTokenProvider.ProviderName);

// PBKDF2-HMAC-SHA512 iterations for parent passwords. Identity's default is 100,000; OWASP recommends at
// least 210,000. About 0.2 s per sign-in. Older hashes are upgraded automatically at the next sign-in.
builder.Services.Configure<PasswordHasherOptions>(options => options.IterationCount = ParentSession.PasswordHashIterations);

// AddIdentityCore doesn't register the security stamp validators that AddIdentity would.
builder.Services.AddScoped<ISecurityStampValidator, SecurityStampValidator<ParentUser>>();
builder.Services.AddScoped<ITwoFactorSecurityStampValidator, TwoFactorSecurityStampValidator<ParentUser>>();
// The cookie and the re-check use the app's clock, so tests can move time forward.
builder.Services.AddOptions<SecurityStampValidatorOptions>()
    .Configure<TimeProvider>((options, time) =>
    {
        options.ValidationInterval = ParentSession.RecheckInterval;
        options.TimeProvider = time;
    });
foreach (var scheme in new[] { IdentityConstants.ApplicationScheme, IdentityConstants.TwoFactorUserIdScheme, IdentityConstants.TwoFactorRememberMeScheme })
{
    builder.Services.AddOptions<CookieAuthenticationOptions>(scheme)
        .Configure<TimeProvider>((options, time) => options.TimeProvider = time);
}
// Email confirmation links. Password reset links have their own, shorter life (PasswordResetTokenProvider).
// Identity checks link expiry against the system clock, not the TimeProvider.
builder.Services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = ParentSession.ConfirmationLinkLifespan);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // Pairing codes are 6 digits, so slow down guessing.
    options.AddPolicy(DeviceEndpoints.PairingRateLimit, ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
    // Registering, forgot password and resending the confirmation link email any address typed in, so limit
    // how many emails one sender can trigger. Sending a sign-in code again shares the limit.
    options.AddPolicy(AuthEndpoints.EmailRateLimit, ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(15) }));
});

builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSignalR().AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(OpenApiSetup.Configure);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ChildLocks>();
builder.Services.AddScoped<BudgetService>();
builder.Services.AddSingleton(EmailSettings.FromConfiguration(builder.Configuration, builder.Environment.ContentRootPath));
builder.Services.AddSingleton<EmailOutbox>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<EmailOutbox>());
builder.Services.AddScoped<AccountEmails>();

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
        options.SwaggerEndpoint("/openapi/v1.json", "Skjermvette API");
        options.RoutePrefix = "docs";
        options.DocumentTitle = "Skjermvette API";
    });
}

app.MapAuthEndpoints();
app.MapAccountEndpoints();
app.MapTwoFactorEndpoints();
app.MapChildEndpoints();
app.MapAppEndpoints();
app.MapDeviceEndpoints();
app.MapHub<BudgetHub>(BudgetHub.Path);

// Dashboard routes like /children/{id}/today are handled in the browser; unknown API paths stay 404.
app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
