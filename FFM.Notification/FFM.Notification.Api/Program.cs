using FFM.Notification.Application.Commands;
using FFM.Notification.Application.Interfaces;
using FFM.Notification.Application.Queries;
using FFM.Notification.Application.Services;
using FFM.Notification.Application.Validators;
using FFM.Notification.Domain.Entities;
using FFM.Notification.Domain.Repositories;
using FFM.Notification.Infrastructure.Data;
using FFM.Notification.Infrastructure.Repositories;
using FFM.Notification.Infrastructure.Services;
using FFM.Notification.Api.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IO;
using System.Security.Cryptography;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add Controllers & HttpContextAccessor
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddEndpointsApiExplorer();

// Swagger Documentation
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "FFM Notification Service API",
        Version = "v1",
        Description = "Device Registration, Push Alert Routing, and Notification Engine for FFM Platform."
    });

    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter Bearer token issued by Identity Service (optional in local Dev mode)"
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

// Database Context: Connect to PostgreSQL (falls back to In-Memory only if configured or offline)
var connectionString = builder.Configuration.GetConnectionString("Postgres")
                    ?? builder.Configuration.GetConnectionString("NotificationDb");
var useInMemory = builder.Configuration.GetValue<bool>("UseInMemoryDatabase");

if (!useInMemory && !string.IsNullOrEmpty(connectionString))
{
    builder.Services.AddDbContext<NotificationDbContext>(options =>
        options.UseNpgsql(connectionString));
}
else
{
    builder.Services.AddDbContext<NotificationDbContext>(options =>
        options.UseInMemoryDatabase("FFM_Notification_DevDb"));
}

// Dependency Injection
builder.Services.AddScoped<IEmployeeDeviceRepository, EmployeeDeviceRepository>();
builder.Services.AddScoped<INotificationTemplateRepository, NotificationTemplateRepository>();
builder.Services.AddScoped<INotificationLogRepository, NotificationLogRepository>();
builder.Services.AddScoped<ITemplateInterpolationEngine, TemplateInterpolationEngine>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IFcmAuthService, FcmAuthService>();
builder.Services.AddSingleton<IPushNotificationDispatcher, FirebasePushDispatcher>();

// MediatR & FluentValidation
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(RegisterDeviceCommand).Assembly));
builder.Services.AddScoped<IValidator<RegisterDeviceCommand>, RegisterDeviceCommandValidator>();
builder.Services.AddScoped<IValidator<DeregisterDeviceCommand>, DeregisterDeviceCommandValidator>();
builder.Services.AddScoped<IValidator<CreateNotificationTemplateCommand>, CreateNotificationTemplateCommandValidator>();
builder.Services.AddScoped<IValidator<UpdateNotificationTemplateCommand>, UpdateNotificationTemplateCommandValidator>();
builder.Services.AddScoped<IValidator<RenderTemplateQuery>, RenderTemplateQueryValidator>();
builder.Services.AddScoped<IValidator<SendNotificationCommand>, SendNotificationCommandValidator>();
builder.Services.AddScoped<IValidator<MarkNotificationReadCommand>, MarkNotificationReadCommandValidator>();
builder.Services.AddScoped<IValidator<GetNotificationHistoryQuery>, GetNotificationHistoryQueryValidator>();
builder.Services.AddScoped<IValidator<ResendNotificationCommand>, ResendNotificationCommandValidator>();
builder.Services.AddScoped<IValidator<GetUnreadNotificationCountQuery>, GetUnreadNotificationCountQueryValidator>();

// Authentication: Load RSA Public Key issued by Identity Service
var publicKeyPath = Path.Combine(builder.Environment.ContentRootPath, "identity-public.key");
RsaSecurityKey? rsaSigningKey = null;

if (File.Exists(publicKeyPath))
{
    try
    {
        var publicKeyBase64 = File.ReadAllText(publicKeyPath).Trim();
        var rsa = RSA.Create();
        rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64), out _);
        rsaSigningKey = new RsaSecurityKey(rsa)
        {
            KeyId = "ffm-rsa-key-1"
        };
    }
    catch { }
}

var jwtSecret = builder.Configuration["JwtSettings:SecretKey"] ?? "FFM_Super_Secret_Key_For_Development_Environment_12345!";
var signingKeys = new List<SecurityKey>();
if (rsaSigningKey != null)
{
    signingKeys.Add(rsaSigningKey);
}
signingKeys.Add(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)));

builder.Services.AddAuthentication("DevOrJwt")
    .AddPolicyScheme("DevOrJwt", "DevOrJwt", options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var token = authHeader.Substring("Bearer ".Length).Trim();
                if (!string.IsNullOrEmpty(token) && token.Split('.').Length == 3)
                {
                    return JwtBearerDefaults.AuthenticationScheme;
                }
            }
            return "DevBypass";
        };
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = signingKeys,
            ValidateIssuer = false, // Allow both ffm.example.com and local
            ValidateAudience = false,
            ValidateLifetime = false, // Prevent Swagger testing blockage when token expires
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    })
    .AddScheme<AuthenticationSchemeOptions, DevBypassAuthHandler>("DevBypass", _ => { });

builder.Services.AddAuthorization();

var app = builder.Build();

// Ensure Database and Seed Initial Templates
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    if (db.Database.IsInMemory())
    {
        db.Database.EnsureCreated();
    }
    else
    {
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""EmployeeDevices"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""EmployeeId"" INT NOT NULL,
                ""DeviceId"" VARCHAR(100) NOT NULL,
                ""Platform"" VARCHAR(20) NOT NULL,
                ""DeviceModel"" VARCHAR(100) NOT NULL,
                ""OsVersion"" VARCHAR(50) NOT NULL,
                ""AppVersion"" VARCHAR(30) NOT NULL,
                ""FcmToken"" VARCHAR(500) NOT NULL,
                ""IsActive"" BOOLEAN NOT NULL DEFAULT TRUE,
                ""RegisteredAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                ""LastActiveAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_EmployeeDevices_DeviceId_Unique"" ON ""EmployeeDevices"" (""DeviceId"");
            CREATE INDEX IF NOT EXISTS ""IX_EmployeeDevices_EmployeeId_IsActive"" ON ""EmployeeDevices"" (""EmployeeId"", ""IsActive"");

            CREATE TABLE IF NOT EXISTS ""NotificationTemplates"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""TemplateCode"" VARCHAR(50) NOT NULL,
                ""Channel"" VARCHAR(20) NOT NULL,
                ""Title"" VARCHAR(200) NULL,
                ""BodyTemplate"" TEXT NOT NULL,
                ""Description"" VARCHAR(300) NULL,
                ""IsActive"" BOOLEAN NOT NULL DEFAULT TRUE,
                ""CreatedByUserId"" INT NOT NULL,
                ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NULL
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_NotificationTemplates_Code_Channel_Unique"" ON ""NotificationTemplates"" (""TemplateCode"", ""Channel"");
            CREATE INDEX IF NOT EXISTS ""IX_NotificationTemplates_Lookup"" ON ""NotificationTemplates"" (""TemplateCode"", ""Channel"", ""IsActive"");

            CREATE TABLE IF NOT EXISTS ""NotificationLogs"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""RecipientEmployeeId"" INT NOT NULL,
                ""TemplateCode"" VARCHAR(50) NOT NULL,
                ""Channel"" VARCHAR(20) NOT NULL,
                ""Title"" VARCHAR(250) NULL,
                ""Body"" TEXT NOT NULL,
                ""FcmMessageId"" VARCHAR(250) NULL,
                ""Status"" VARCHAR(30) NOT NULL,
                ""ErrorMessage"" TEXT NULL,
                ""DispatchedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                ""IsRead"" BOOLEAN NOT NULL DEFAULT FALSE,
                ""ReadAt"" TIMESTAMP WITH TIME ZONE NULL
            );

            CREATE INDEX IF NOT EXISTS ""IX_NotificationLogs_RecipientEmployeeId"" ON ""NotificationLogs"" (""RecipientEmployeeId"");
            CREATE INDEX IF NOT EXISTS ""IX_NotificationLogs_DispatchedAt"" ON ""NotificationLogs"" (""DispatchedAt"");
            CREATE INDEX IF NOT EXISTS ""IX_NotificationLogs_Status"" ON ""NotificationLogs"" (""Status"");
            CREATE INDEX IF NOT EXISTS ""IX_NotificationLogs_Recipient_IsRead"" ON ""NotificationLogs"" (""RecipientEmployeeId"", ""IsRead"");
        ");
    }

    if (!db.NotificationTemplates.Any())
    {
        db.NotificationTemplates.AddRange(
            new NotificationTemplate("TASK_ASSIGNED", "PUSH", "New Task Assigned: {{TaskNumber}}", "Hi {{EmployeeName}}, new task #{{TaskNumber}} at {{SiteName}} has been assigned to you. Priority: {{Priority}}.", "Task assignment push", 1),
            new NotificationTemplate("TASK_CANCELLED", "PUSH", "Task Cancelled: {{TaskNumber}}", "Task #{{TaskNumber}} has been cancelled. Reason: {{CancelReason}}.", "Task cancellation alert", 1),
            new NotificationTemplate("CHECKIN_REMINDER", "PUSH", "Upcoming Site Visit: {{SiteName}}", "Reminder: Scheduled visit at {{SiteName}} in {{RemainingMinutes}} minutes.", "Checkin reminder", 1),
            new NotificationTemplate("GEOFENCE_BREACH", "PUSH", "⚠️ Geofence Alert: {{SiteName}}", "You have moved outside the designated site perimeter for {{SiteName}} by {{BreachDistanceMeters}} meters.", "Geofence alert", 1)
        );
        db.SaveChanges();
    }

    if (!db.NotificationLogs.Any())
    {
        db.NotificationLogs.AddRange(
            new NotificationLog(9, "TASK_ASSIGNED", "PUSH", "New Field Task Assigned: TSK-2026-089", "Water pipeline pressure inspection assigned at Muttrah Corniche. Priority: HIGH.", "fcm_msg_001", "DELIVERED", null, false),
            new NotificationLog(9, "GEOFENCE_ALERT", "PUSH", "⚠️ Geofence Boundary Alert", "Inspector entered restricted excavation zone #4 at Ruwi municipal facility without safety clearance.", "fcm_msg_002", "DELIVERED", null, false),
            new NotificationLog(9, "SECURITY_2FA", "EMAIL", "Security Alert: 2FA Login Verified", "Successful 2FA verification from Chrome on Windows for nambukamali@gmail.com.", "email_msg_003", "DELIVERED", null, false),
            new NotificationLog(9, "SHIFT_REMINDER", "PUSH", "Upcoming Shift Reminder: Zone A", "Municipal morning maintenance shift scheduled today from 08:00 AM to 04:00 PM.", "fcm_msg_004", "DELIVERED", null, true, DateTime.UtcNow.AddHours(-2)),
            new NotificationLog(9, "SYSTEM_UPDATE", "SMS", "System Maintenance Completed", "Muscat Municipality Field Operations platform version 2.4 update completed successfully.", "sms_msg_005", "DELIVERED", null, true, DateTime.UtcNow.AddHours(-5))
        );
        db.SaveChanges();
    }
}

app.UseStaticFiles();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "FFM Notification API v1");
    c.RoutePrefix = "swagger";
});

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "FFM.Notification" }));

app.MapControllers();

app.Run();

// Make Program accessible for WebApplicationFactory in integration tests
public partial class Program { }
