using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FFM.Notification.Application.Interfaces;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FFM.Notification.Infrastructure.Services;

public class FcmAuthService : IFcmAuthService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<FcmAuthService> _logger;
    private readonly string _credentialsPath;

    public FcmAuthService(IConfiguration configuration, ILogger<FcmAuthService> logger)
    {
        _configuration = configuration;
        _logger = logger;

        // Resolve credentials path from config, AppContext, or current directory
        var configuredPath = _configuration["Firebase:CredentialsPath"];
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
        {
            _credentialsPath = configuredPath;
        }
        else
        {
            var baseDirPath = Path.Combine(AppContext.BaseDirectory, "firebase-service-account.json");
            var currentDirPath = Path.Combine(Directory.GetCurrentDirectory(), "firebase-service-account.json");
            var apiDirPath = Path.Combine(Directory.GetCurrentDirectory(), "FFM.Notification.Api", "firebase-service-account.json");

            if (File.Exists(baseDirPath))
                _credentialsPath = baseDirPath;
            else if (File.Exists(currentDirPath))
                _credentialsPath = currentDirPath;
            else if (File.Exists(apiDirPath))
                _credentialsPath = apiDirPath;
            else
                _credentialsPath = baseDirPath; // fallback
        }
    }

    public async Task<FcmTokenResult> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_credentialsPath))
        {
            _logger.LogError("Firebase credentials file not found at: {Path}", _credentialsPath);
            throw new FileNotFoundException($"Firebase service account credentials file was not found at '{_credentialsPath}'.");
        }

        try
        {
            // Parse project_id from json file
            var jsonContent = await File.ReadAllTextAsync(_credentialsPath, cancellationToken);
            using var doc = JsonDocument.Parse(jsonContent);
            var projectId = doc.RootElement.TryGetProperty("project_id", out var projProp) 
                ? projProp.GetString() ?? "ffm-notification-api"
                : "ffm-notification-api";

            // Authenticate with Google OAuth2 servers using Service Account private key
            using var stream = File.OpenRead(_credentialsPath);
            var credential = GoogleCredential.FromStream(stream)
                .CreateScoped("https://www.googleapis.com/auth/firebase.messaging");

            var token = await credential.UnderlyingCredential.GetAccessTokenForRequestAsync(cancellationToken: cancellationToken);

            _logger.LogInformation("Successfully generated Google OAuth2 Bearer Access Token for project: {ProjectId}", projectId);

            return new FcmTokenResult
            {
                ProjectId = projectId,
                TokenType = "Bearer",
                AccessToken = token,
                ExpiresInSeconds = 3600
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate Google OAuth2 token using Firebase credentials at: {Path}", _credentialsPath);
            throw;
        }
    }
}
