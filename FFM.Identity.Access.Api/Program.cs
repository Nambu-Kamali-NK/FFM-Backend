// File: Program.cs
using FFM.Identity.Access.Api.Middleware;
using FFM.Identity.Access.Api.Services;
using FFM.Identity.Access.Domain.Options;
using FFM.Identity.Access.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHealthChecks();

// Suppress Kestrel Server header
builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
});

// Load configuration sections
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<MfaOptions>(builder.Configuration.GetSection("Mfa"));
builder.Services.Configure<DeviceBindingOptions>(builder.Configuration.GetSection("DeviceBinding"));
builder.Services.Configure<RateLimitOptions>(builder.Configuration.GetSection("RateLimit"));

// DbContext
var connectionString = builder.Configuration.GetConnectionString("Postgres");
builder.Services.AddDbContext<IdentityDbContext>(options =>
    options.UseNpgsql(connectionString));

// Authentication (JWT)
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtOptions = jwtSection.Get<JwtOptions>() ?? new JwtOptions();

// Resolve RSA Private Key securely from Environment, Secret Store, or Local Secret File
string? resolvedPrivateKey = jwtOptions.RsaPrivateKey;
if (string.IsNullOrWhiteSpace(resolvedPrivateKey))
{
    resolvedPrivateKey = Environment.GetEnvironmentVariable("JWT_RSA_PRIVATE_KEY")
        ?? Environment.GetEnvironmentVariable("Jwt__RsaPrivateKey")
        ?? builder.Configuration["Jwt:RsaPrivateKey"];
}

var keyPath = builder.Configuration["Jwt:RsaPrivateKeyPath"]
    ?? Environment.GetEnvironmentVariable("JWT_RSA_PRIVATE_KEY_PATH");

if (string.IsNullOrWhiteSpace(resolvedPrivateKey) && !string.IsNullOrWhiteSpace(keyPath) && File.Exists(keyPath))
{
    resolvedPrivateKey = File.ReadAllText(keyPath).Trim();
}

if (string.IsNullOrWhiteSpace(resolvedPrivateKey))
{
    var candidatePaths = new List<string> { "/run/secrets/identity-private.key" };

    if (builder.Environment.IsDevelopment())
    {
        candidatePaths.Add(Path.Combine(builder.Environment.ContentRootPath, "identity-private.key"));
        candidatePaths.Add(Path.Combine(builder.Environment.ContentRootPath, "..", "identity-private.key"));
        candidatePaths.Add(Path.Combine(AppContext.BaseDirectory, "identity-private.key"));
    }

    foreach (var candidate in candidatePaths)
    {
        if (File.Exists(candidate))
        {
            resolvedPrivateKey = File.ReadAllText(candidate).Trim();
            break;
        }
    }
}

if (string.IsNullOrWhiteSpace(resolvedPrivateKey))
{
    throw new InvalidOperationException(
        "JWT RSA Private Key is not configured. Provide it via environment variable (JWT_RSA_PRIVATE_KEY), secret file (Jwt:RsaPrivateKeyPath), or key vault.");
}

jwtOptions.RsaPrivateKey = resolvedPrivateKey;
builder.Services.PostConfigure<JwtOptions>(opts => opts.RsaPrivateKey = resolvedPrivateKey);

var rsaPrivate = RSA.Create();
var keyBytes = Convert.FromBase64String(jwtOptions.RsaPrivateKey);
rsaPrivate.ImportPkcs8PrivateKey(keyBytes, out _);
var rsaPublic = RSA.Create();
rsaPublic.ImportParameters(rsaPrivate.ExportParameters(false));
var signingKey = new RsaSecurityKey(rsaPublic)
{
    KeyId = "ffm-rsa-key-1"
};

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("JwtBearer");
                logger.LogWarning("Authentication failed: {Message}", context.Exception.Message);
                return Task.CompletedTask;
            }
        };
    });

//Authorization
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanCreateUser", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole("SYSTEM_ADMIN", "ADMIN");
    });
});

// Services
builder.Services.AddHttpClient();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IOAuthService, OAuthService>();
builder.Services.AddScoped<IDeviceService, DeviceService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IEmailSender, EmailSender>();
builder.Services.AddScoped<ISmsSender, SmsSender>();
// (Other services stubbed for now)

// Controllers and OpenAPI
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter your JWT token."
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("AuthPolicy", context => RateLimitPartition.GetFixedWindowLimiter<string>(
        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            AutoReplenishment = true,
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        }));
    options.RejectionStatusCode = 429;
});

// CORS configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("FfmCorsPolicy", policy =>
    {
        policy.SetIsOriginAllowed(origin =>
                  string.IsNullOrEmpty(origin) ||
                  origin.StartsWith("http://localhost") ||
                  origin.StartsWith("https://localhost") ||
                  origin.EndsWith(".netlify.app") ||
                  origin == "https://muscat-municipality-ffm.netlify.app")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Centralized Problem Details exception handling
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Security headers middleware
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "no-referrer");
    context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'; style-src 'self' 'unsafe-inline'; script-src 'self' 'unsafe-inline'; img-src 'self' data:;");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    if (context.Request.IsHttps)
    {
        context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
    }
    await next();
});

app.UseHttpsRedirection();
app.UseCors("FfmCorsPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapHealthChecks("/health");
app.MapControllers().RequireRateLimiting("AuthPolicy");

app.Run();
