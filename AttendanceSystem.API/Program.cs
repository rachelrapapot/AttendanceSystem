using AspNetCoreRateLimit;
using AttendanceSystem.API.Data;
using AttendanceSystem.API.Middleware;
using AttendanceSystem.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using System.Text;

// Prevent .NET from remapping standard JWT claim names to long URN names
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

var builder = WebApplication.CreateBuilder(args);

// ── Startup configuration validation ────────────────────────────────────────
ValidateRequiredConfig(builder.Configuration, builder.Environment);

// ── Database ─────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseSqlServer(
        builder.Configuration.GetConnectionString("Default"),
        sqlOpts => sqlOpts.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null)));

// ── HTTP client for TimeAPI ───────────────────────────────────────────────────
builder.Services.AddHttpClient<ITimeService, TimeService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
});

// ── Application services ──────────────────────────────────────────────────────
builder.Services.AddSingleton<AppMetrics>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuditService, AuditService>();

// Background service: probes TimeAPI every minute; fires LogCritical after 5 min downtime
builder.Services.AddHostedService<TimeApiHealthMonitor>();

// ── JWT authentication — reads token from httpOnly cookie ─────────────────────
//
// Why httpOnly cookies instead of localStorage/sessionStorage?
//   The httpOnly flag (set in AuthController.SetCookie) instructs the browser
//   never to expose the cookie to JavaScript — document.cookie cannot read it.
//   A successful XSS attack can therefore not exfiltrate the token to an
//   attacker's server. Without httpOnly the token lives in JS-accessible
//   storage and any injected script can simply read and forward it.
//
// Why SameSite=Strict?
//   SameSite=Strict (also set in AuthController.SetCookie) tells the browser
//   to withhold this cookie from every cross-site request — form POSTs from
//   another domain, navigations triggered by a third-party page, img/script
//   fetches — none of them carry the cookie. Classic CSRF exploits the fact
//   that browsers send cookies automatically with cross-origin requests;
//   SameSite=Strict removes that assumption entirely, so a forged request
//   from evil.com arrives at the API without credentials and is rejected as
//   401 before any handler runs.
//
// Why two tokens (access + refresh) instead of one long-lived token?
//   Access token  — 15-minute JWT validated on every request (stateless).
//                   Short lifetime caps the damage window: a token leaked via
//                   a server log, a brief network intercept, or a compromised
//                   reverse proxy becomes useless within minutes.
//   Refresh token — long-lived, but stored as a SHA-256 hash in the database.
//                   Because it exists in the DB, it can be hard-revoked the
//                   moment a logout or suspicious-activity event is detected —
//                   no token-blocklist infrastructure required. The access
//                   token alone cannot be revoked (it's stateless), which is
//                   exactly why it must be short-lived.
//   A single long-lived JWT would give an attacker days of access after theft;
//   splitting into a short-lived access token and a revocable refresh token
//   keeps the exploitation window small without requiring constant re-login.
//
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]!)),
            ValidateIssuer   = true,
            ValidIssuer      = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience    = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            // Zero skew enforces the exact 15-minute access-token lifetime;
            // the default 5-minute tolerance would silently extend it to ~20 min.
            ClockSkew        = TimeSpan.Zero,
            RoleClaimType    = ClaimTypes.Role,
            NameClaimType    = ClaimTypes.NameIdentifier
        };
        opts.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Cookies["access_token"];
                if (!string.IsNullOrEmpty(token))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ── CORS — exact origin only, credentials allowed ─────────────────────────────
builder.Services.AddCors(opts =>
{
    opts.AddDefaultPolicy(policy =>
    {
        var origin = builder.Configuration["Cors:AllowedOrigin"]!;
        policy.WithOrigins(origin)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// ── Rate limiting ─────────────────────────────────────────────────────────────
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();
builder.Services.AddInMemoryRateLimiting();

// ── Swagger / OpenAPI ─────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Attendance System API",
        Version = "v1",
        Description = """
            REST API for employee attendance tracking.

            **Authentication** — this API uses httpOnly JWT cookies.
            To authenticate in Swagger UI:
            1. Call `POST /api/auth/login` with valid credentials.
            2. The browser stores the `access_token` cookie automatically.
            3. All subsequent requests include the cookie — no manual token entry needed.

            Access tokens expire after **15 minutes**. Call `POST /api/auth/refresh`
            to renew without re-entering credentials.
            """
    });

    // Cookie-based security scheme — matches the access_token httpOnly cookie
    c.AddSecurityDefinition("CookieAuth", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Cookie,
        Name = "access_token",
        Description = "JWT access token stored in the 'access_token' httpOnly cookie. " +
                      "Obtained by calling POST /api/auth/login; the browser stores and sends it automatically."
    });

    // Apply the cookie scheme globally so every endpoint shows the lock icon
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "CookieAuth"
                }
            },
            Array.Empty<string>()
        }
    });

    // Wire up XML doc comments from the build-time generated XML file
    var xmlPath = Path.Combine(AppContext.BaseDirectory,
        $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    c.IncludeXmlComments(xmlPath);
});

builder.Services.AddControllers();

var app = builder.Build();

// ── Middleware pipeline (order matters) ───────────────────────────────────────
app.UseMiddleware<ExceptionMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>(); // applies a relaxed CSP for /swagger/* paths
app.UseMiddleware<RequestLoggingMiddleware>(); // after exception handler so errors are still logged

// Swagger UI — placed before rate limiting so that UI/JSON requests are not throttled.
// withCredentials: true makes the browser send the httpOnly access_token cookie with every
// Swagger request; call POST /api/auth/login once and all subsequent requests authenticate
// automatically without touching the Authorize dialog.
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Attendance System API v1");
    c.ConfigObject.AdditionalItems["withCredentials"] = true;
});

app.UseIpRateLimiting();
app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await SeedData.InitializeAsync(app.Services, app.Configuration);

app.Run();

// ── Helpers ───────────────────────────────────────────────────────────────────
static void ValidateRequiredConfig(IConfiguration config, IWebHostEnvironment env)
{
    // Secret keys and their env-var names (ASP.NET Core maps __ → :).
    // In Production these must arrive via env vars; in Development appsettings fallback is allowed.
    (string Key, string EnvVar)[] secretKeys =
    [
        ("ConnectionStrings:Default", "ConnectionStrings__Default"),
        ("Jwt:Secret",                "Jwt__Secret"),
        ("Seed:AdminPassword",        "Seed__AdminPassword"),
    ];

    // Non-secret keys — must be present in any environment, any source is fine
    var nonSecrets = new[] { "Jwt:Issuer", "Jwt:Audience", "Cors:AllowedOrigin" };
    var missing = nonSecrets.Where(k => string.IsNullOrWhiteSpace(config[k])).ToList();

    if (env.IsProduction())
    {
        // Production: each secret must be supplied as an environment variable.
        // Relying on appsettings.json for secrets in Production is disallowed.
        foreach (var (key, envVar) in secretKeys)
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(envVar)))
                missing.Add($"{key} (required env var: {envVar})");
        }
    }
    else
    {
        // Development / Staging: appsettings.Development.json fallback is acceptable
        missing.AddRange(secretKeys
            .Where(s => string.IsNullOrWhiteSpace(config[s.Key]))
            .Select(s => s.Key));
    }

    if (missing.Count > 0)
    {
        var hint = env.IsProduction()
            ? "In Production every secret must be set as an environment variable — never in appsettings.json."
            : "In Development set values via environment variables or appsettings.Development.json.";
        throw new InvalidOperationException(
            $"Missing required configuration: {string.Join(", ", missing)}. {hint}");
    }

    var jwtSecret = config["Jwt:Secret"]!;
    if (jwtSecret.Length < 32)
        throw new InvalidOperationException(
            "Jwt:Secret must be at least 32 characters. Use a cryptographically random value.");
}

// Expose Program for WebApplicationFactory in tests
public partial class Program { }
