using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using FFM.Workforce.Application.Interfaces;
using FFM.Workforce.Application.DTOs;
using FFM.Workforce.Application.Behaviors;
using FFM.Workforce.Application.Validators;
using FFM.Workforce.Api.Middleware;
using FFM.Workforce.Api.Converters;
using FFM.Workforce.Infrastructure;
using FFM.Workforce.Infrastructure.Repositories;
using FFM.Workforce.Infrastructure.Services;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);

// Suppress Kestrel Server header
builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
});

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new UtcDateTimeJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new NullableUtcDateTimeJsonConverter());
    });
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "FFM Workforce API",
        Version = "v1",
        Description = "Workforce Management Service for FFM Platform."
    });

    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter: Bearer {your JWT access token}"
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

// Authentication (JWT) – placeholder key; real key should be provided via configuration.
var publicKeyPath = Path.Combine(
    builder.Environment.ContentRootPath,
    "identity-public.key");

var publicKeyBase64 = File.ReadAllText(publicKeyPath).Trim();

var rsa = RSA.Create();
rsa.ImportSubjectPublicKeyInfo(
    Convert.FromBase64String(publicKeyBase64),
    out _);

var signingKey = new RsaSecurityKey(rsa)
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
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = false,
        ValidateIssuerSigningKey = true,

        IssuerSigningKeys = new SecurityKey[]
        {
            signingKey,
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes("SuperSecretDevelopmentKey123!_FFM_Production_Key_2026_Secure"))
        },

        ClockSkew = TimeSpan.FromMinutes(5)
    };

    options.Events = new JwtBearerEvents
    {
        OnChallenge = async context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";

            var response = ApiResponse<object>.Fail(
                "UNAUTHORIZED",
                "Invalid or missing authentication token",
                "Authentication required");

            await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
        },
        OnForbidden = async context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";

            var response = ApiResponse<object>.Fail(
                "FORBIDDEN",
                "You do not have permission to perform this action",
                "Forbidden");

            await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
        }
    };
});

// Authorization – only the three roles may create employees.
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanCreateEmployee", policy =>
        policy.RequireAssertion(context =>
            context.User.HasClaim(c => (c.Type == "role" || c.Type == System.Security.Claims.ClaimTypes.Role) &&
                (c.Value == "SYSTEM_ADMIN" || c.Value == "ADMIN" || c.Value == "MANAGER"))));

    options.AddPolicy("CanEditEmployee", policy =>
        policy.RequireAssertion(context =>
            context.User.HasClaim(c => (c.Type == "role" || c.Type == System.Security.Claims.ClaimTypes.Role) &&
                (c.Value == "SYSTEM_ADMIN" || c.Value == "ADMIN" || c.Value == "MANAGER" || c.Value == "SUPERVISOR"))));
});

// Rate limiting
var permitLimit = builder.Configuration.GetValue<int>("RateLimit:PermitLimit", 60);
var windowMinutes = builder.Configuration.GetValue<int>("RateLimit:WindowMinutes", 1);

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("WorkforcePolicy", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromMinutes(windowMinutes),
            AutoReplenishment = true,
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// FluentValidation: register all validators from the Application assembly
AssemblyScanner.FindValidatorsInAssembly(typeof(CreateEmployeeCommandValidator).Assembly)
    .ForEach(item => builder.Services.AddScoped(item.InterfaceType, item.ValidatorType));

// Application & Infrastructure services
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(FFM.Workforce.Application.Commands.CreateEmployeeCommand).Assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
});

builder.Services.AddScoped<IEmployeeRepository, EmployeeRepository>();
builder.Services.AddScoped<IAttendanceRepository, AttendanceRepository>();
builder.Services.AddScoped<ILeaveRepository, LeaveRepository>();
builder.Services.AddScoped<IPermissionRepository, PermissionRepository>();
builder.Services.AddScoped<IShiftRepository, ShiftRepository>();
builder.Services.AddScoped<IRosterRepository, RosterRepository>();
builder.Services.AddScoped<IOutboxRepository, OutboxRepository>();
builder.Services.AddScoped<IOrgUnitRepository, OrgUnitRepository>();
builder.Services.AddHttpClient<IGeofenceValidationClient, GeofenceValidationClient>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
// Forward DbContext to WorkforceDbContext to satisfy Application layer dependencies
builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<WorkforceDbContext>());
// EF Core – use PostgreSQL connection string from configuration or environment variable.
var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
    ?? throw new InvalidOperationException("PostgreSQL connection string is not configured. Configure 'ConnectionStrings:Postgres' or environment variable.");

builder.Services.AddDbContext<WorkforceDbContext>(options =>
    options.UseNpgsql(connectionString));

// CORS policy
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

// Global Exception Handling (prevents stack trace / internal details disclosure)
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Security headers middleware
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; style-src 'self' 'unsafe-inline'; script-src 'self' 'unsafe-inline'; img-src 'self' data:;";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    if (context.Request.IsHttps)
    {
        context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    }
    await next();
});

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "FFM Workforce API v1");
    c.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();
app.UseCors("FfmCorsPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapHealthChecks("/health");
app.MapControllers().RequireRateLimiting("WorkforcePolicy");

app.Run();

public partial class Program { }
