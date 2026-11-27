using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using FFM.FieldOperations.Application.Commands;
using FFM.FieldOperations.Application.DTOs;
using FFM.FieldOperations.Application.Interfaces;
using FFM.FieldOperations.Application.Services;
using FFM.FieldOperations.Domain.Interfaces;
using FFM.FieldOperations.Infrastructure.Data;
using FFM.FieldOperations.Infrastructure.Repositories;
using FFM.FieldOperations.Api.Converters;
using FFM.FieldOperations.Api.Middleware;
using FFM.FieldOperations.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Suppress Kestrel Server header (A6 Security Misconfiguration Defense)
builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
});

// Rate limiting (Sliding window 60 req/min)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddSlidingWindowLimiter("FieldOperationsPolicy", opt =>
    {
        opt.PermitLimit = 5000;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.SegmentsPerWindow = 6;
        opt.QueueLimit = 0;
    });
});

// High-Performance HTTP Response Compression (Brotli & Gzip) & In-Memory Response Caching
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
});
builder.Services.AddResponseCaching();

// Add services
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
        Title = "FFM Field Operations API",
        Version = "v1",
        Description = "Field Operations, Client Sites, and Task/Visit Management Service for FFM Platform."
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

// Configure RSA JWT Authentication
var publicKeyPath = Path.Combine(builder.Environment.ContentRootPath, "identity-public.key");
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
        OnChallenge = async context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";

            var response = ApiResponse<object>.Fail(
                "UNAUTHORIZED",
                "Invalid or missing authentication token",
                "Authentication required");

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        },
        OnForbidden = async context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";

            var response = ApiResponse<object>.Fail(
                "FORBIDDEN",
                "You do not have permission to perform this action",
                "Forbidden");

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    };
});

// Register MediatR
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(CreateClientSiteCommand).Assembly);
});

// DI Repositories & Services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IClientSiteRepository, ClientSiteRepository>();
builder.Services.AddScoped<ITaskRepository, TaskRepository>();
builder.Services.AddScoped<IVisitRepository, VisitRepository>();
builder.Services.AddScoped<IEvidenceTypeRepository, EvidenceTypeRepository>();
builder.Services.AddScoped<IEvidenceFieldValidator, EvidenceFieldValidator>();

// Push Notification Dispatcher HTTP Client (Connects to FFM.Notification.Api on port 5040 / Workforce on 5010)
builder.Services.AddHttpClient<ITaskNotificationDispatcher, TaskNotificationDispatcher>(client =>
{
    var notifUrl = builder.Configuration["Services:NotificationApiUrl"]
                   ?? builder.Configuration["Services:WorkforceApiUrl"]
                   ?? Environment.GetEnvironmentVariable("NOTIFICATION_API_URL")
                   ?? "http://localhost:5040";
    client.BaseAddress = new Uri(notifUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddScoped<IS3StorageService>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var s3Section = config.GetSection("AWS:S3");
    return new AwsS3StorageService(
        s3Section["BucketName"] ?? "ffm-field-evidence-prod",
        s3Section["Region"] ?? "ap-south-1",
        s3Section["AccessKey"],
        s3Section["SecretKey"],
        int.TryParse(s3Section["DefaultExpiryMinutes"], out var exp) ? exp : 15);
});

// PostgreSQL Database
var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
    ?? throw new InvalidOperationException("PostgreSQL connection string is not configured.");

builder.Services.AddDbContext<FieldOperationsDbContext>(options =>
    options.UseNpgsql(connectionString));

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("FfmCorsPolicy", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Ensure Database & Tables Exist
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<FieldOperationsDbContext>();
    dbContext.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ""ClientSites"" (
            ""Id"" SERIAL PRIMARY KEY,
            ""SiteCode"" VARCHAR(50) NOT NULL,
            ""Name"" VARCHAR(150) NOT NULL,
            ""ContactPerson"" VARCHAR(100) NULL,
            ""ContactPhone"" VARCHAR(30) NULL,
            ""ContactEmail"" VARCHAR(150) NULL,
            ""Latitude"" DECIMAL(10, 7) NOT NULL,
            ""Longitude"" DECIMAL(10, 7) NOT NULL,
            ""RadiusMeters"" INT NOT NULL DEFAULT 150,
            ""Address"" VARCHAR(300) NOT NULL,
            ""City"" VARCHAR(100) NULL,
            ""State"" VARCHAR(100) NULL,
            ""PostalCode"" VARCHAR(20) NULL,
            ""IsActive"" BOOLEAN NOT NULL DEFAULT TRUE,
            ""CreatedByUserId"" INT NOT NULL,
            ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
            ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
        );

        CREATE UNIQUE INDEX IF NOT EXISTS ""IX_ClientSites_SiteCode_Unique"" ON ""ClientSites"" (UPPER(""SiteCode""));
        CREATE INDEX IF NOT EXISTS ""IX_ClientSites_Coordinates"" ON ""ClientSites"" (""Latitude"", ""Longitude"");
        CREATE INDEX IF NOT EXISTS ""IX_ClientSites_IsActive"" ON ""ClientSites"" (""IsActive"");

        CREATE TABLE IF NOT EXISTS ""Tasks"" (
            ""Id"" SERIAL PRIMARY KEY,
            ""TaskNumber"" VARCHAR(50) NOT NULL,
            ""Title"" VARCHAR(200) NOT NULL,
            ""Description"" TEXT,
            ""Priority"" VARCHAR(20) NOT NULL DEFAULT 'MEDIUM',
            ""Status"" VARCHAR(30) NOT NULL DEFAULT 'CREATED',
            ""ClientSiteId"" INT NOT NULL REFERENCES ""ClientSites"" (""Id"") ON DELETE RESTRICT,
            ""AssignedEmployeeId"" INT NULL,
            ""ScheduledStart"" TIMESTAMP WITH TIME ZONE NOT NULL,
            ""ScheduledEnd"" TIMESTAMP WITH TIME ZONE NOT NULL,
            ""CreatedByUserId"" INT NOT NULL,
            ""CancellationReason"" VARCHAR(500) NULL,
            ""CancelledAt"" TIMESTAMP WITH TIME ZONE NULL,
            ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
            ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
        );

        CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Tasks_TaskNumber"" ON ""Tasks"" (""TaskNumber"");
        CREATE INDEX IF NOT EXISTS ""IX_Tasks_ClientSiteId"" ON ""Tasks"" (""ClientSiteId"");
        CREATE INDEX IF NOT EXISTS ""IX_Tasks_AssignedEmployeeId_Status"" ON ""Tasks"" (""AssignedEmployeeId"", ""Status"");
        CREATE INDEX IF NOT EXISTS ""IX_Tasks_Status"" ON ""Tasks"" (""Status"");

        CREATE TABLE IF NOT EXISTS ""Visits"" (
            ""Id"" SERIAL PRIMARY KEY,
            ""TaskId"" INT NOT NULL REFERENCES ""Tasks"" (""Id"") ON DELETE CASCADE,
            ""EmployeeId"" INT NOT NULL,
            ""CheckInTime"" TIMESTAMP WITH TIME ZONE NOT NULL,
            ""CheckInLatitude"" DECIMAL(10, 7) NOT NULL,
            ""CheckInLongitude"" DECIMAL(10, 7) NOT NULL,
            ""CheckInDistanceMeters"" DECIMAL(8, 2) NOT NULL,
            ""IsWithinGeofence"" BOOLEAN NOT NULL DEFAULT TRUE,
            ""CheckOutTime"" TIMESTAMP WITH TIME ZONE NULL,
            ""CheckOutLatitude"" DECIMAL(10, 7) NULL,
            ""CheckOutLongitude"" DECIMAL(10, 7) NULL,
            ""DurationMinutes"" DECIMAL(6, 2) NULL,
            ""CompletionNotes"" TEXT NULL,
            ""CustomerName"" VARCHAR(100) NULL,
            ""CustomerSignatureUrl"" VARCHAR(500) NULL,
            ""CustomerRating"" INT NULL,
            ""Status"" VARCHAR(30) NOT NULL DEFAULT 'CHECKED_IN',
            ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
        );

        CREATE INDEX IF NOT EXISTS ""IX_Visits_TaskId"" ON ""Visits"" (""TaskId"");
        CREATE INDEX IF NOT EXISTS ""IX_Visits_CheckInTime"" ON ""Visits"" (""CheckInTime"");
        CREATE INDEX IF NOT EXISTS ""IX_Visits_Employee_Status"" ON ""Visits"" (""EmployeeId"", ""Status"");

        CREATE TABLE IF NOT EXISTS ""VisitEvidences"" (
            ""Id"" SERIAL PRIMARY KEY,
            ""VisitId"" INT NOT NULL REFERENCES ""Visits"" (""Id"") ON DELETE CASCADE,
            ""EvidenceType"" VARCHAR(30) NOT NULL,
            ""FileName"" VARCHAR(255) NULL,
            ""FolderPath"" VARCHAR(255) NULL,
            ""FileUrl"" VARCHAR(500) NOT NULL,
            ""ThumbnailUrl"" VARCHAR(500) NULL,
            ""Remarks"" VARCHAR(500) NULL,
            ""CapturedLatitude"" DECIMAL(10, 7) NULL,
            ""CapturedLongitude"" DECIMAL(10, 7) NULL,
            ""CapturedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
        );

        ALTER TABLE ""VisitEvidences"" ADD COLUMN IF NOT EXISTS ""FileName"" VARCHAR(255) NULL;
        ALTER TABLE ""VisitEvidences"" ADD COLUMN IF NOT EXISTS ""FolderPath"" VARCHAR(255) NULL;

        CREATE INDEX IF NOT EXISTS ""IX_VisitEvidences_VisitId"" ON ""VisitEvidences"" (""VisitId"");
        CREATE INDEX IF NOT EXISTS ""IX_VisitEvidences_Type"" ON ""VisitEvidences"" (""EvidenceType"");
        CREATE INDEX IF NOT EXISTS ""IX_VisitEvidences_CapturedAt"" ON ""VisitEvidences"" (""CapturedAt"");
    ");
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "FFM Field Operations API v1");
    c.RoutePrefix = "swagger";
});

// Global Exception Handling (prevents stack trace / internal details disclosure)
app.UseMiddleware<ExceptionHandlingMiddleware>();

// High-Speed Wire Optimization: Compress JSON/Protobuf responses
app.UseResponseCompression();

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

app.UseCors("FfmCorsPolicy");
app.UseResponseCaching();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapHealthChecks("/health");
app.MapControllers().RequireRateLimiting("FieldOperationsPolicy");

app.Run();
