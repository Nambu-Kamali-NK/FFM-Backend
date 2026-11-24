using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FFM.GPS.Tracking.Application.Commands;
using FFM.GPS.Tracking.Domain.Entities;
using FFM.GPS.Tracking.Infrastructure;
using FFM.GPS.Tracking.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FFM.GPS.Tracking.Tests;

public class BreadcrumbTrackingTests
{
    private GpsDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<GpsDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new GpsDbContext(options);
    }

    [Fact]
    public async Task Handle_ValidBreadcrumb_SavesToDatabaseAndReturnsDto()
    {
        // Arrange
        var db = CreateInMemoryDbContext("Breadcrumb_Success_Test");
        var repository = new BreadcrumbRepository(db);
        var handler = new CreateBreadcrumbCommandHandler(repository);

        var command = new CreateBreadcrumbCommand
        {
            UserId = 202,
            Latitude = 13.082745m,
            Longitude = 80.270718m,
            AccuracyMeters = 5.5m,
            SpeedKmh = 24.5m,
            HeadingDegrees = 180.0m,
            AltitudeMeters = 15.0m,
            BatteryPercentage = 85,
            ActivityType = "DRIVING",
            RecordedAt = DateTime.UtcNow
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(202, result.UserId);
        Assert.True(result.BreadcrumbId > 0);

        // Verify persisted in DB
        var saved = await repository.GetLatestByUserIdAsync(202);
        Assert.NotNull(saved);
        Assert.Equal(13.082745m, saved.Latitude);
        Assert.Equal(80.270718m, saved.Longitude);
        Assert.Equal(5.5m, saved.AccuracyMeters);
        Assert.Equal("DRIVING", saved.ActivityType);
    }

    [Fact]
    public async Task Handle_InvalidLatitude_ThrowsArgumentException()
    {
        var db = CreateInMemoryDbContext("Breadcrumb_InvalidLat_Test");
        var repository = new BreadcrumbRepository(db);
        var handler = new CreateBreadcrumbCommandHandler(repository);

        var command = new CreateBreadcrumbCommand
        {
            UserId = 202,
            Latitude = 95.5m, // Out of [-90, 90] bounds
            Longitude = 80.270718m,
            AccuracyMeters = 5.0m,
            RecordedAt = DateTime.UtcNow
        };

        await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_FutureTimestamp_ThrowsArgumentException()
    {
        var db = CreateInMemoryDbContext("Breadcrumb_FutureTime_Test");
        var repository = new BreadcrumbRepository(db);
        var handler = new CreateBreadcrumbCommandHandler(repository);

        var command = new CreateBreadcrumbCommand
        {
            UserId = 202,
            Latitude = 13.0827m,
            Longitude = 80.2707m,
            AccuracyMeters = 5.0m,
            RecordedAt = DateTime.UtcNow.AddDays(2) // 2 days in the future
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("future", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_TimestampOlderThan7Days_ThrowsArgumentException()
    {
        var db = CreateInMemoryDbContext("Breadcrumb_AncientTime_Test");
        var repository = new BreadcrumbRepository(db);
        var handler = new CreateBreadcrumbCommandHandler(repository);

        var command = new CreateBreadcrumbCommand
        {
            UserId = 202,
            Latitude = 13.0827m,
            Longitude = 80.2707m,
            AccuracyMeters = 5.0m,
            RecordedAt = DateTime.UtcNow.AddDays(-10) // 10 days old
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("7 days", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_InvalidLongitude_ThrowsArgumentException()
    {
        var db = CreateInMemoryDbContext("Breadcrumb_InvalidLon_Test");
        var repository = new BreadcrumbRepository(db);
        var handler = new CreateBreadcrumbCommandHandler(repository);

        var command = new CreateBreadcrumbCommand
        {
            UserId = 202,
            Latitude = 13.082745m,
            Longitude = 185.0m, // Out of [-180, 180] bounds
            AccuracyMeters = 5.0m,
            RecordedAt = DateTime.UtcNow
        };

        await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_DegradedAccuracy_ThrowsArgumentException()
    {
        var db = CreateInMemoryDbContext("Breadcrumb_BadAccuracy_Test");
        var repository = new BreadcrumbRepository(db);
        var handler = new CreateBreadcrumbCommandHandler(repository);

        var command = new CreateBreadcrumbCommand
        {
            UserId = 202,
            Latitude = 13.082745m,
            Longitude = 80.270718m,
            AccuracyMeters = 150.0m, // > 100 meters threshold
            RecordedAt = DateTime.UtcNow
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("acceptable threshold", ex.Message);
    }

    [Fact]
    public async Task Repository_GetByEmployeeAndDate_FiltersCorrectly()
    {
        var db = CreateInMemoryDbContext("Breadcrumb_DateFilter_Test");
        var repository = new BreadcrumbRepository(db);

        var targetDate = new DateTime(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc);
        var anotherDate = new DateTime(2026, 10, 11, 10, 0, 0, DateTimeKind.Utc);

        await repository.AddAsync(new LocationBreadcrumb
        {
            UserId = 202,
            Latitude = 13.0827m,
            Longitude = 80.2707m,
            AccuracyMeters = 5m,
            RecordedAt = targetDate
        });

        await repository.AddAsync(new LocationBreadcrumb
        {
            UserId = 202,
            Latitude = 13.0830m,
            Longitude = 80.2710m,
            AccuracyMeters = 5m,
            RecordedAt = anotherDate
        });

        await repository.SaveChangesAsync();

        var results = await repository.GetByUserAndDateAsync(202, new DateTime(2026, 10, 12));
        Assert.Single(results);
    }

    [Fact]
    public void GenerateValidToken_WritesTokenForLiveTesting()
    {
        var privateKeyBase64 = File.ReadAllLines("../../../../../.env")
            .FirstOrDefault(l => l.StartsWith("JWT_RSA_PRIVATE_KEY="))?
            .Replace("JWT_RSA_PRIVATE_KEY=", "")
            .Trim();

        if (string.IsNullOrEmpty(privateKeyBase64))
            return;

        using var rsa = RSA.Create();
        var keyBytes = Convert.FromBase64String(privateKeyBase64);
        rsa.ImportPkcs8PrivateKey(keyBytes, out _);

        var header = "{\"alg\":\"RS256\",\"kid\":\"ffm-rsa-key-1\",\"typ\":\"JWT\"}";
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = $"{{\"sub\":\"202\",\"userId\":\"202\",\"user_id\":\"202\",\"nameid\":\"202\",\"client_id\":\"ffm-mobile\",\"role\":\"EMPLOYEE\",\"nbf\":{now},\"exp\":{now + 86400 * 365},\"iat\":{now},\"iss\":\"https://ffm.example.com\",\"aud\":\"ffm-client\"}}";

        static string Base64Url(byte[] data) => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var headerB64 = Base64Url(Encoding.UTF8.GetBytes(header));
        var payloadB64 = Base64Url(Encoding.UTF8.GetBytes(payload));
        var dataToSign = Encoding.UTF8.GetBytes($"{headerB64}.{payloadB64}");
        var signature = rsa.SignData(dataToSign, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var token = $"{headerB64}.{payloadB64}.{Base64Url(signature)}";

        File.WriteAllText("live_token.txt", token);

        // Also write Manager token
        var managerPayload = $"{{\"sub\":\"999\",\"userId\":\"999\",\"user_id\":\"999\",\"nameid\":\"999\",\"client_id\":\"ffm-portal\",\"role\":\"MANAGER\",\"nbf\":{now},\"exp\":{now + 86400 * 365},\"iat\":{now},\"iss\":\"https://ffm.example.com\",\"aud\":\"ffm-client\"}}";
        var mgrPayloadB64 = Base64Url(Encoding.UTF8.GetBytes(managerPayload));
        var mgrDataToSign = Encoding.UTF8.GetBytes($"{headerB64}.{mgrPayloadB64}");
        var mgrSig = rsa.SignData(mgrDataToSign, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var mgrToken = $"{headerB64}.{mgrPayloadB64}.{Base64Url(mgrSig)}";
        File.WriteAllText("manager_token.txt", mgrToken);

        // Also write Employee 468 token
        var emp468Payload = $"{{\"sub\":\"468\",\"userId\":\"468\",\"user_id\":\"468\",\"nameid\":\"468\",\"client_id\":\"ffm-mobile\",\"role\":\"EMPLOYEE\",\"nbf\":{now},\"exp\":{now + 86400 * 365},\"iat\":{now},\"iss\":\"https://ffm.example.com\",\"aud\":\"ffm-client\"}}";
        var emp468PayloadB64 = Base64Url(Encoding.UTF8.GetBytes(emp468Payload));
        var emp468DataToSign = Encoding.UTF8.GetBytes($"{headerB64}.{emp468PayloadB64}");
        var emp468Sig = rsa.SignData(emp468DataToSign, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var emp468Token = $"{headerB64}.{emp468PayloadB64}.{Base64Url(emp468Sig)}";
        File.WriteAllText("emp468_token.txt", emp468Token);

        Assert.NotEmpty(token);
    }
}


