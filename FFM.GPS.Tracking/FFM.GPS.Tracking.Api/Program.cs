using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using FFM.GPS.Tracking.Api.Hubs;
using FFM.GPS.Tracking.Api.Services;
using FFM.GPS.Tracking.Application.Commands;
using FFM.GPS.Tracking.Application.Interfaces;
using FFM.GPS.Tracking.Domain.Interfaces;
using FFM.GPS.Tracking.Domain.Services;
using FFM.GPS.Tracking.Infrastructure;
using FFM.GPS.Tracking.Infrastructure.Repositories;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Suppress Kestrel Server header
builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
});

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "FFM GPS Tracking & Geofencing API",
        Version = "v1",
        Description = "Spatial math engine, geofence definitions, and real-time location validation service for FFM Platform."
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

// JWT Authentication Setup
var publicKeyPath = Path.Combine(builder.Environment.ContentRootPath, "identity-public.key");
if (File.Exists(publicKeyPath))
{
    var publicKeyBase64 = File.ReadAllText(publicKeyPath).Trim();
    var rsa = RSA.Create();
    rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64), out _);

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
            ValidateIssuer = true,
            ValidIssuer = "https://ffm.example.com",
            ValidateAudience = true,
            ValidAudience = "ffm-client",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // Extract JWT token from query string during SignalR WebSocket connection handshake
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/tracking"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";

                var response = FFM.GPS.Tracking.Application.DTOs.ApiResponse<object>.Fail(
                    "UNAUTHORIZED",
                    "Invalid or missing authentication token",
                    "Authentication required");

                await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";

                var response = FFM.GPS.Tracking.Application.DTOs.ApiResponse<object>.Fail(
                    "FORBIDDEN",
                    "You do not have permission to perform this action",
                    "Forbidden");

                await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
            }
        };
    });
}
else
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme);
}

builder.Services.AddAuthorization();

// MediatR & Domain Services
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(ValidateGeofenceCommand).Assembly);
});

builder.Services.AddSingleton<IGeofenceValidationService, GeofenceValidationService>();
builder.Services.AddScoped<IGeofenceRepository, GeofenceRepository>();
builder.Services.AddScoped<IBreadcrumbRepository, BreadcrumbRepository>();
builder.Services.AddScoped<IGeofenceBreachRepository, GeofenceBreachRepository>();
builder.Services.AddSingleton<IGeofenceStateCache, FFM.GPS.Tracking.Infrastructure.Services.RedisGeofenceStateCache>();
builder.Services.AddScoped<FFM.GPS.Tracking.Application.Services.IGeofenceBreachDetector, FFM.GPS.Tracking.Application.Services.GeofenceBreachDetector>();
builder.Services.AddScoped<FFM.GPS.Tracking.Application.Services.IRouteAnalyticsService, FFM.GPS.Tracking.Application.Services.RouteAnalyticsService>();

// PostgreSQL Database Context
var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
    ?? throw new InvalidOperationException("PostgreSQL connection string is not configured. Configure 'ConnectionStrings:Postgres' or environment variable.");

builder.Services.AddDbContext<GpsDbContext>(options =>
    options.UseNpgsql(connectionString));

// Redis Connection & SignalR Real-Time Streaming Setup
var redisConnString = Environment.GetEnvironmentVariable("Redis__ConnectionString")
    ?? builder.Configuration.GetConnectionString("Redis")
    ?? "localhost:6379,abortConnect=false,connectTimeout=5000";

IConnectionMultiplexer? redisMultiplexer = null;
try
{
    redisMultiplexer = ConnectionMultiplexer.Connect(redisConnString);
    builder.Services.AddSingleton<IConnectionMultiplexer>(redisMultiplexer);
}
catch (Exception ex)
{
    Console.WriteLine($"[WARN] Could not connect to Redis at {redisConnString}: {ex.Message}. SignalR running in local hub mode.");
}

var signalRBuilder = builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
});

if (redisMultiplexer != null && redisMultiplexer.IsConnected)
{
    signalRBuilder.AddStackExchangeRedis(redisConnString, options =>
    {
        options.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("ffm:tracking:signalr");
    });
}

// Register Location Stream Publisher
builder.Services.AddScoped<ILocationStreamPublisher, SignalRRedisLocationPublisher>();

// CORS Policy (supporting SignalR WebSocket credentials)
builder.Services.AddCors(options =>
{
    options.AddPolicy("FfmCorsPolicy", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

app.UseMiddleware<FFM.GPS.Tracking.Api.Middleware.ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "FFM GPS Tracking API v1");
    c.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();
app.UseCors("FfmCorsPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

// Map SignalR Real-Time Tracking Hub
app.MapHub<TrackingHub>("/hubs/tracking");

app.Run();

public partial class Program { }
