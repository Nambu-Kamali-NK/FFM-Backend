using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FFM.GPS.Tracking.Api.Hubs;
using FFM.GPS.Tracking.Api.Services;
using FFM.GPS.Tracking.Application.Commands;
using FFM.GPS.Tracking.Application.DTOs;
using FFM.GPS.Tracking.Application.Interfaces;
using FFM.GPS.Tracking.Domain.Entities;
using FFM.GPS.Tracking.Domain.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace FFM.GPS.Tracking.Tests;

public class SignalRAndRedisStreamingTests
{
    // =========================================================================
    // 1. SignalR & Redis Publisher Tests
    // =========================================================================

    [Fact]
    public async Task PublishLocationAsync_ValidBreadcrumb_CachesInRedisWithCorrectTTLAndGeoIndex()
    {
        // Arrange
        var hubContextMock = new Mock<IHubContext<TrackingHub, ITrackingClient>>();
        var clientsMock = new Mock<IHubClients<ITrackingClient>>();
        var clientProxyMock = new Mock<ITrackingClient>();
        var redisMultiplexerMock = new Mock<IConnectionMultiplexer>();
        var redisDbMock = new Mock<IDatabase>();
        var loggerMock = new Mock<ILogger<SignalRRedisLocationPublisher>>();

        hubContextMock.Setup(h => h.Clients).Returns(clientsMock.Object);
        clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(clientProxyMock.Object);

        redisMultiplexerMock.Setup(r => r.IsConnected).Returns(true);
        redisMultiplexerMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(redisDbMock.Object);

        var publisher = new SignalRRedisLocationPublisher(hubContextMock.Object, loggerMock.Object, redisMultiplexerMock.Object);

        var breadcrumb = new LocationBreadcrumbStreamDto
        {
            UserId = 215,
            Latitude = 11.0827m,
            Longitude = 76.9957m,
            AccuracyMeters = 5m,
            SpeedKmh = 25m,
            HeadingDegrees = 180m,
            BatteryPercentage = 90,
            ActivityType = "DRIVING",
            RecordedAt = DateTime.UtcNow
        };

        // Act
        await publisher.PublishLocationAsync(breadcrumb);

        // Assert Redis Cache
        redisDbMock.Verify(db => db.HashSetAsync(
            "tracking:user:215:latest",
            It.Is<HashEntry[]>(entries => entries.Length == 9),
            It.IsAny<CommandFlags>()), Times.Once);

        redisDbMock.Verify(db => db.KeyExpireAsync(
            "tracking:user:215:latest",
            TimeSpan.FromSeconds(120),
            It.IsAny<ExpireWhen>(),
            It.IsAny<CommandFlags>()), Times.Once);

        // Assert Redis Geospatial Indexing
        redisDbMock.Verify(db => db.GeoAddAsync(
            "tracking:tenant:default:geo",
            (double)76.9957m,
            (double)11.0827m,
            "215",
            It.IsAny<CommandFlags>()), Times.Once);

        // Assert SignalR Push to user group and supervisors
        clientsMock.Verify(c => c.Group("user_215"), Times.Once);
        clientsMock.Verify(c => c.Group("supervisors"), Times.Once);
        clientProxyMock.Verify(cp => cp.ReceiveLocationUpdate(breadcrumb), Times.Exactly(2));
    }

    [Fact]
    public async Task PublishLocationAsync_WhenRedisThrowsException_CircuitBreakerLogsWarningAndContinuesSignalRBroadcast()
    {
        // Arrange
        var hubContextMock = new Mock<IHubContext<TrackingHub, ITrackingClient>>();
        var clientsMock = new Mock<IHubClients<ITrackingClient>>();
        var clientProxyMock = new Mock<ITrackingClient>();
        var redisMultiplexerMock = new Mock<IConnectionMultiplexer>();
        var redisDbMock = new Mock<IDatabase>();
        var loggerMock = new Mock<ILogger<SignalRRedisLocationPublisher>>();

        hubContextMock.Setup(h => h.Clients).Returns(clientsMock.Object);
        clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(clientProxyMock.Object);

        redisMultiplexerMock.Setup(r => r.IsConnected).Returns(true);
        redisMultiplexerMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(redisDbMock.Object);

        // Simulate Redis Connection Failure
        redisDbMock.Setup(db => db.HashSetAsync(It.IsAny<RedisKey>(), It.IsAny<HashEntry[]>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new TimeoutException("Redis connection timed out"));

        var publisher = new SignalRRedisLocationPublisher(hubContextMock.Object, loggerMock.Object, redisMultiplexerMock.Object);

        var breadcrumb = new LocationBreadcrumbStreamDto
        {
            UserId = 215,
            Latitude = 11.0827m,
            Longitude = 76.9957m,
            RecordedAt = DateTime.UtcNow
        };

        // Act & Assert (Should not throw)
        var exception = await Record.ExceptionAsync(() => publisher.PublishLocationAsync(breadcrumb));
        Assert.Null(exception);

        // SignalR still dispatched despite Redis error (Graceful degradation)
        clientProxyMock.Verify(cp => cp.ReceiveLocationUpdate(breadcrumb), Times.Exactly(2));
    }

    [Fact]
    public async Task PublishUserOfflineAsync_BroadcastsOfflineToUserAndSupervisorGroups()
    {
        // Arrange
        var hubContextMock = new Mock<IHubContext<TrackingHub, ITrackingClient>>();
        var clientsMock = new Mock<IHubClients<ITrackingClient>>();
        var clientProxyMock = new Mock<ITrackingClient>();
        var loggerMock = new Mock<ILogger<SignalRRedisLocationPublisher>>();

        hubContextMock.Setup(h => h.Clients).Returns(clientsMock.Object);
        clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(clientProxyMock.Object);

        var publisher = new SignalRRedisLocationPublisher(hubContextMock.Object, loggerMock.Object);

        var offlineEvent = new UserOfflineEventDto
        {
            UserId = 215,
            LastKnownLatitude = 11.0827m,
            LastKnownLongitude = 76.9957m,
            WentOfflineAt = DateTime.UtcNow,
            Reason = "SIGNAL_TIMEOUT"
        };

        // Act
        await publisher.PublishUserOfflineAsync(offlineEvent);

        // Assert
        clientProxyMock.Verify(cp => cp.UserWentOffline(offlineEvent), Times.Exactly(2));
    }

    // =========================================================================
    // 2. TrackingHub IDOR & Security Tests
    // =========================================================================

    [Fact]
    public async Task TrackingHub_JoinTeamStream_WhenCallerIsEmployee_ThrowsHubException()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<TrackingHub>>();
        var hub = new TrackingHub(loggerMock.Object);

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("userId", "215"),
            new Claim(ClaimTypes.Role, "EMPLOYEE")
        }, "TestAuth"));

        var contextMock = new Mock<HubCallerContext>();
        contextMock.Setup(c => c.User).Returns(user);
        contextMock.Setup(c => c.ConnectionId).Returns("conn_123");
        hub.Context = contextMock.Object;

        // Act & Assert: Employee role cannot join team stream
        var ex = await Assert.ThrowsAsync<HubException>(() => hub.JoinTeamStream(5));
        Assert.Contains("Forbidden", ex.Message);
    }

    [Fact]
    public async Task TrackingHub_JoinTeamStream_WhenCallerIsSupervisor_AddsToTeamGroup()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<TrackingHub>>();
        var hub = new TrackingHub(loggerMock.Object);

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("userId", "999"),
            new Claim(ClaimTypes.Role, "SUPERVISOR")
        }, "TestAuth"));

        var contextMock = new Mock<HubCallerContext>();
        contextMock.Setup(c => c.User).Returns(user);
        contextMock.Setup(c => c.ConnectionId).Returns("conn_123");
        hub.Context = contextMock.Object;

        var groupsMock = new Mock<IGroupManager>();
        hub.Groups = groupsMock.Object;

        // Act
        await hub.JoinTeamStream(5);

        // Assert
        groupsMock.Verify(g => g.AddToGroupAsync("conn_123", "team_5", default), Times.Once);
    }

    [Fact]
    public async Task TrackingHub_TrackUser_WhenEmployeeTracksOtherUser_ThrowsHubException()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<TrackingHub>>();
        var hub = new TrackingHub(loggerMock.Object);

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("userId", "215"),
            new Claim(ClaimTypes.Role, "EMPLOYEE")
        }, "TestAuth"));

        var contextMock = new Mock<HubCallerContext>();
        contextMock.Setup(c => c.User).Returns(user);
        contextMock.Setup(c => c.ConnectionId).Returns("conn_123");
        hub.Context = contextMock.Object;

        // Act & Assert: Employee 215 cannot spy on User 300
        var ex = await Assert.ThrowsAsync<HubException>(() => hub.TrackUser(300));
        Assert.Contains("Forbidden", ex.Message);
    }

    [Fact]
    public async Task TrackingHub_TrackUser_WhenEmployeeTracksSelf_AddsToUserGroup()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<TrackingHub>>();
        var hub = new TrackingHub(loggerMock.Object);

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("userId", "215"),
            new Claim(ClaimTypes.Role, "EMPLOYEE")
        }, "TestAuth"));

        var contextMock = new Mock<HubCallerContext>();
        contextMock.Setup(c => c.User).Returns(user);
        contextMock.Setup(c => c.ConnectionId).Returns("conn_123");
        hub.Context = contextMock.Object;

        var groupsMock = new Mock<IGroupManager>();
        hub.Groups = groupsMock.Object;

        // Act: Employee 215 tracking self is allowed
        await hub.TrackUser(215);

        // Assert
        groupsMock.Verify(g => g.AddToGroupAsync("conn_123", "user_215", default), Times.Once);
    }

    // =========================================================================
    // 3. CreateBreadcrumbCommandHandler Dual-Path Resilience Tests
    // =========================================================================

    [Fact]
    public async Task Handle_WithStreamPublisher_InvokesPublishLocationAsync()
    {
        // Arrange
        var repoMock = new Mock<IBreadcrumbRepository>();
        var publisherMock = new Mock<ILocationStreamPublisher>();

        var handler = new CreateBreadcrumbCommandHandler(repoMock.Object, publisherMock.Object);

        var command = new CreateBreadcrumbCommand
        {
            UserId = 215,
            Latitude = 11.0827m,
            Longitude = 76.9957m,
            AccuracyMeters = 5m,
            SpeedKmh = 30m,
            HeadingDegrees = 90m,
            BatteryPercentage = 95,
            ActivityType = "DRIVING",
            RecordedAt = DateTime.UtcNow
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert Cold Path (Database write)
        repoMock.Verify(r => r.AddAsync(It.IsAny<LocationBreadcrumb>(), It.IsAny<CancellationToken>()), Times.Once);
        repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        // Assert Fast Path (Real-time stream)
        publisherMock.Verify(p => p.PublishLocationAsync(
            It.Is<LocationBreadcrumbStreamDto>(d => d.UserId == 215 && d.Latitude == 11.0827m),
            It.IsAny<CancellationToken>()), Times.Once);

        Assert.Equal(215, result.UserId);
    }

    [Fact]
    public async Task Handle_WhenStreamPublisherThrowsException_ColdPathStillPersistsToDatabaseAndReturnsSuccess()
    {
        // Arrange
        var repoMock = new Mock<IBreadcrumbRepository>();
        var publisherMock = new Mock<ILocationStreamPublisher>();

        // Simulate streaming error (e.g. network/socket failure)
        publisherMock.Setup(p => p.PublishLocationAsync(It.IsAny<LocationBreadcrumbStreamDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Streaming channel broke"));

        var handler = new CreateBreadcrumbCommandHandler(repoMock.Object, publisherMock.Object);

        var command = new CreateBreadcrumbCommand
        {
            UserId = 215,
            Latitude = 11.0827m,
            Longitude = 76.9957m,
            AccuracyMeters = 5m,
            RecordedAt = DateTime.UtcNow
        };

        // Act (Must not throw error to client)
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert Database save succeeded
        repoMock.Verify(r => r.AddAsync(It.IsAny<LocationBreadcrumb>(), It.IsAny<CancellationToken>()), Times.Once);
        repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(215, result.UserId);
    }

    [Fact]
    public async Task TrackingHub_SendBreadcrumb_WhenValid_DispatchesCommandAndReturnsSuccess()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<TrackingHub>>();
        var mediatorMock = new Mock<MediatR.IMediator>();

        mediatorMock.Setup(m => m.Send(It.IsAny<CreateBreadcrumbCommand>(), default))
            .ReturnsAsync(new BreadcrumbResponseDto
            {
                BreadcrumbId = 100,
                UserId = 217,
                RecordedAt = DateTime.UtcNow,
                ReceivedAt = DateTime.UtcNow
            });

        var hub = new TrackingHub(loggerMock.Object, mediatorMock.Object);

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("userId", "217"),
            new Claim(ClaimTypes.Role, "EMPLOYEE")
        }, "TestAuth"));

        var contextMock = new Mock<HubCallerContext>();
        contextMock.Setup(c => c.User).Returns(user);
        contextMock.Setup(c => c.ConnectionId).Returns("conn_socket_1");
        hub.Context = contextMock.Object;

        var request = new CreateBreadcrumbRequest
        {
            UserId = 217,
            Latitude = 87.9796m,
            Longitude = 88.6745m,
            AccuracyMeters = 5m,
            SpeedKmh = 50m,
            ActivityType = "Driving"
        };

        // Act: Send over WebSocket hub
        var response = await hub.SendBreadcrumb(request);

        // Assert
        Assert.True(response.Success);
        Assert.Equal(100, response.Data.BreadcrumbId);
        Assert.Equal(217, response.Data.UserId);
        mediatorMock.Verify(m => m.Send(It.Is<CreateBreadcrumbCommand>(c => c.UserId == 217 && c.Latitude == 87.9796m), default), Times.Once);
    }

    [Fact]
    public async Task TrackingHub_SendBreadcrumb_WhenEmployeeSpoofsOtherUser_ThrowsHubException()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<TrackingHub>>();
        var mediatorMock = new Mock<MediatR.IMediator>();
        var hub = new TrackingHub(loggerMock.Object, mediatorMock.Object);

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("userId", "217"),
            new Claim(ClaimTypes.Role, "EMPLOYEE")
        }, "TestAuth"));

        var contextMock = new Mock<HubCallerContext>();
        contextMock.Setup(c => c.User).Returns(user);
        contextMock.Setup(c => c.ConnectionId).Returns("conn_socket_2");
        hub.Context = contextMock.Object;

        var request = new CreateBreadcrumbRequest
        {
            UserId = 999, // Attempting to post for user 999
            Latitude = 87.9796m,
            Longitude = 88.6745m,
            AccuracyMeters = 5m
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<HubException>(() => hub.SendBreadcrumb(request));
        Assert.Contains("Forbidden", ex.Message);
    }
}

