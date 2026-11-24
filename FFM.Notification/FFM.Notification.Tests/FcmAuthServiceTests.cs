using System;
using System.IO;
using System.Threading.Tasks;
using FFM.Notification.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FFM.Notification.Tests;

public class FcmAuthServiceTests
{
    [Fact]
    public async Task GetAccessTokenAsync_WhenValidCredentialsExist_ReturnsRealGoogleAccessToken()
    {
        // Find firebase-service-account.json in Api project or current directory
        var candidatePaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "firebase-service-account.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "firebase-service-account.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "FFM.Notification.Api", "firebase-service-account.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "FFM.Notification.Api", "firebase-service-account.json"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "FFM.Notification.Api", "firebase-service-account.json"))
        };

        string? foundPath = null;
        foreach (var path in candidatePaths)
        {
            if (File.Exists(path))
            {
                foundPath = path;
                break;
            }
        }

        if (foundPath == null)
        {
            // If file is not located in test runner dir, skip asserting live Google call
            return;
        }

        var inMemorySettings = new System.Collections.Generic.Dictionary<string, string?>
        {
            { "Firebase:CredentialsPath", foundPath }
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var service = new FcmAuthService(configuration, NullLogger<FcmAuthService>.Instance);

        // Act
        var result = await service.GetAccessTokenAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ffm-notification-api", result.ProjectId);
        Assert.Equal("Bearer", result.TokenType);
        Assert.NotNull(result.AccessToken);
        Assert.StartsWith("ya29.", result.AccessToken); // Real Google OAuth2 access tokens begin with ya29.
        Assert.True(result.AccessToken.Length > 50);
    }
}

