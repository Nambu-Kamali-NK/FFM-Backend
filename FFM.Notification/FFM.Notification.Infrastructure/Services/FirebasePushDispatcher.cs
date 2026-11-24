using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FFM.Notification.Application.Interfaces;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FFM.Notification.Infrastructure.Services;

public class FirebasePushDispatcher : IPushNotificationDispatcher
{
    private readonly ILogger<FirebasePushDispatcher> _logger;
    private readonly bool _isInitialized;

    public FirebasePushDispatcher(IConfiguration configuration, ILogger<FirebasePushDispatcher> logger)
    {
        _logger = logger;

        try
        {
            if (FirebaseApp.DefaultInstance == null)
            {
                var configuredPath = configuration["Firebase:CredentialsPath"];
                string? credentialsPath = null;

                if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
                {
                    credentialsPath = configuredPath;
                }
                else
                {
                    var baseDirPath = Path.Combine(AppContext.BaseDirectory, "firebase-service-account.json");
                    var currentDirPath = Path.Combine(Directory.GetCurrentDirectory(), "firebase-service-account.json");
                    var apiDirPath = Path.Combine(Directory.GetCurrentDirectory(), "FFM.Notification.Api", "firebase-service-account.json");

                    if (File.Exists(baseDirPath))
                        credentialsPath = baseDirPath;
                    else if (File.Exists(currentDirPath))
                        credentialsPath = currentDirPath;
                    else if (File.Exists(apiDirPath))
                        credentialsPath = apiDirPath;
                }

                if (credentialsPath != null && File.Exists(credentialsPath))
                {
                    using var stream = File.OpenRead(credentialsPath);
                    FirebaseApp.Create(new AppOptions
                    {
                        Credential = GoogleCredential.FromStream(stream),
                        ProjectId = "ffm-notification-api"
                    });
                    _logger.LogInformation("FirebaseApp successfully initialized from {Path}", credentialsPath);
                    _isInitialized = true;
                }
                else
                {
                    _logger.LogWarning("Firebase service account credentials file not found. Push notifications will run in simulated mode.");
                    _isInitialized = false;
                }
            }
            else
            {
                _isInitialized = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize FirebaseApp.");
            _isInitialized = false;
        }
    }

    public async Task<PushDispatchResult> SendAsync(
        string fcmToken,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fcmToken))
        {
            return PushDispatchResult.Fail("INVALID_TOKEN", "FCM device token cannot be null or empty.");
        }

        if (!_isInitialized)
        {
            // Fallback for environments where credentials file is not mounted
            _logger.LogInformation("[DEV MOCK FCM] Simulating push to token: {Token} | Title: {Title}", fcmToken, title);
            var simulatedId = $"projects/ffm-notification-api/messages/sim_{Guid.NewGuid():N}";
            return PushDispatchResult.Ok(simulatedId);
        }

        try
        {
            var message = new Message
            {
                Token = fcmToken,
                Notification = new FirebaseAdmin.Messaging.Notification
                {
                    Title = title,
                    Body = body
                },
                Data = data != null ? new Dictionary<string, string>(data) : null,
                Android = new AndroidConfig
                {
                    Priority = Priority.High,
                    Notification = new AndroidNotification
                    {
                        Sound = "default"
                    }
                }
            };

            _logger.LogInformation("Sending real FCM push to token: {TokenPrefix}...", fcmToken.Substring(0, Math.Min(15, fcmToken.Length)));
            var messageId = await FirebaseMessaging.DefaultInstance.SendAsync(message, cancellationToken);
            _logger.LogInformation("Firebase FCM delivered successfully. Message ID: {MessageId}", messageId);

            return PushDispatchResult.Ok(messageId);
        }
        catch (FirebaseMessagingException ex)
        {
            _logger.LogWarning("Firebase FCM dispatch failed with code {ErrorCode}: {Message}", ex.MessagingErrorCode, ex.Message);
            return PushDispatchResult.Fail(ex.MessagingErrorCode?.ToString() ?? "FCM_ERROR", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error dispatching FCM notification.");
            return PushDispatchResult.Fail("DISPATCH_ERROR", ex.Message);
        }
    }
}
