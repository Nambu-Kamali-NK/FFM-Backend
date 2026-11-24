using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFM.GPS.Tracking.Application.Commands;
using FFM.GPS.Tracking.Application.DTOs;
using FFM.GPS.Tracking.Application.Handlers;
using FFM.GPS.Tracking.Application.Interfaces;
using FFM.GPS.Tracking.Application.Queries;
using FFM.GPS.Tracking.Application.Services;
using FFM.GPS.Tracking.Domain.Entities;
using FFM.GPS.Tracking.Domain.Interfaces;
using FFM.GPS.Tracking.Domain.Services;
using Moq;
using Xunit;

namespace FFM.GPS.Tracking.Tests
{
    public class GeofenceBreachDetectionTests
    {
        private readonly Mock<IGeofenceRepository> _geofenceRepoMock;
        private readonly Mock<IGeofenceBreachRepository> _breachRepoMock;
        private readonly Mock<ILocationStreamPublisher> _streamPublisherMock;
        private readonly IGeofenceValidationService _validationService;
        private readonly InMemoryGeofenceStateCache _stateCache;
        private readonly List<GeofenceBreachEvent> _savedBreaches;

        public GeofenceBreachDetectionTests()
        {
            _geofenceRepoMock = new Mock<IGeofenceRepository>();
            _breachRepoMock = new Mock<IGeofenceBreachRepository>();
            _streamPublisherMock = new Mock<ILocationStreamPublisher>();
            _validationService = new GeofenceValidationService();
            _stateCache = new InMemoryGeofenceStateCache();
            _savedBreaches = new List<GeofenceBreachEvent>();

            _breachRepoMock.Setup(r => r.AddAsync(It.IsAny<GeofenceBreachEvent>(), It.IsAny<CancellationToken>()))
                .Callback<GeofenceBreachEvent, CancellationToken>((b, _) => _savedBreaches.Add(b))
                .Returns(Task.CompletedTask);

            _breachRepoMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        private Geofence CreateCircularGeofence(int id = 1, string locationRef = "SITE-001", double lat = 12.9716, double lng = 77.5946, double radius = 100.0)
        {
            var geofence = new Geofence(locationRef, "HQ Bangalore", "CIRCULAR", lat, lng, radius, bufferMeters: 10.0);
            typeof(Geofence).GetProperty(nameof(Geofence.Id))?.SetValue(geofence, id);
            return geofence;
        }

        [Fact]
        public async Task EvaluateBreadcrumbAsync_WhenNoAssignedGeofences_GeneratesNoAlerts()
        {
            // Arrange
            _geofenceRepoMock.Setup(r => r.GetAssignedGeofencesByEmployeeIdAsync(101))
                .ReturnsAsync(new List<Geofence>());

            var detector = new GeofenceBreachDetector(
                _geofenceRepoMock.Object,
                _breachRepoMock.Object,
                _validationService,
                _stateCache,
                _streamPublisherMock.Object);

            var breadcrumb = new LocationBreadcrumb
            {
                UserId = 101,
                Latitude = 12.9716m,
                Longitude = 77.5946m,
                AccuracyMeters = 10m,
                RecordedAt = DateTime.UtcNow
            };

            // Act
            var alerts = await detector.EvaluateBreadcrumbAsync(breadcrumb);

            // Assert
            Assert.Empty(alerts);
            Assert.Empty(_savedBreaches);
            _streamPublisherMock.Verify(s => s.PublishGeofenceAlertAsync(It.IsAny<GeofenceAlertDto>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task EvaluateBreadcrumbAsync_WhenInitialPingIsInside_InitializesCacheAndGeneratesNoBreach()
        {
            // Arrange
            var geofence = CreateCircularGeofence();
            _geofenceRepoMock.Setup(r => r.GetAssignedGeofencesByEmployeeIdAsync(101))
                .ReturnsAsync(new List<Geofence> { geofence });

            var detector = new GeofenceBreachDetector(
                _geofenceRepoMock.Object,
                _breachRepoMock.Object,
                _validationService,
                _stateCache,
                _streamPublisherMock.Object);

            // Exactly at center
            var breadcrumb = new LocationBreadcrumb
            {
                UserId = 101,
                Latitude = 12.9716m,
                Longitude = 77.5946m,
                AccuracyMeters = 5m,
                RecordedAt = DateTime.UtcNow
            };

            // Act
            var alerts = await detector.EvaluateBreadcrumbAsync(breadcrumb);

            // Assert
            Assert.Empty(alerts);
            Assert.Empty(_savedBreaches);
            var state = await _stateCache.GetStateAsync(101, geofence.Id);
            Assert.Equal("INSIDE", state);
        }

        [Fact]
        public async Task EvaluateBreadcrumbAsync_WhenTransitionFromInsideToOutside_TriggersExitBreachAndAlert()
        {
            // Arrange
            var geofence = CreateCircularGeofence(lat: 12.9716, lng: 77.5946, radius: 100.0);
            _geofenceRepoMock.Setup(r => r.GetAssignedGeofencesByEmployeeIdAsync(101))
                .ReturnsAsync(new List<Geofence> { geofence });

            // Seed prior state as INSIDE
            await _stateCache.SetStateAsync(101, geofence.Id, "INSIDE");

            var detector = new GeofenceBreachDetector(
                _geofenceRepoMock.Object,
                _breachRepoMock.Object,
                _validationService,
                _stateCache,
                _streamPublisherMock.Object);

            // Move to coordinates ~2000m away
            var breadcrumb = new LocationBreadcrumb
            {
                UserId = 101,
                Latitude = 12.9900m,
                Longitude = 77.5946m,
                AccuracyMeters = 8m,
                SpeedKmh = 35m,
                RecordedAt = DateTime.UtcNow
            };

            // Act
            var alerts = await detector.EvaluateBreadcrumbAsync(breadcrumb);

            // Assert
            Assert.Single(alerts);
            var alert = alerts[0];
            Assert.Equal("EXIT", alert.BreachType);
            Assert.Equal(101, alert.UserId);
            Assert.Equal(geofence.Id, alert.GeofenceId);
            Assert.Equal("HIGH", alert.Severity);
            Assert.Contains("exited assigned geofence", alert.Message);

            Assert.Single(_savedBreaches);
            var breach = _savedBreaches[0];
            Assert.Equal("EXIT", breach.BreachType);
            Assert.Equal(101, breach.UserId);
            Assert.False(breach.IsAcknowledged);

            // Verify cache updated to OUTSIDE
            var newState = await _stateCache.GetStateAsync(101, geofence.Id);
            Assert.Equal("OUTSIDE", newState);

            // Verify real-time SignalR/Redis alert publication
            _streamPublisherMock.Verify(s => s.PublishGeofenceAlertAsync(
                It.Is<GeofenceAlertDto>(a => a.UserId == 101 && a.BreachType == "EXIT"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task EvaluateBreadcrumbAsync_WhenContinuingOutside_DebouncesAndDoesNotSpamDuplicateAlerts()
        {
            // Arrange
            var geofence = CreateCircularGeofence(lat: 12.9716, lng: 77.5946, radius: 100.0);
            _geofenceRepoMock.Setup(r => r.GetAssignedGeofencesByEmployeeIdAsync(101))
                .ReturnsAsync(new List<Geofence> { geofence });

            // Seed prior state as OUTSIDE
            await _stateCache.SetStateAsync(101, geofence.Id, "OUTSIDE");

            var detector = new GeofenceBreachDetector(
                _geofenceRepoMock.Object,
                _breachRepoMock.Object,
                _validationService,
                _stateCache,
                _streamPublisherMock.Object);

            // Position remains far outside
            var breadcrumb = new LocationBreadcrumb
            {
                UserId = 101,
                Latitude = 12.9950m,
                Longitude = 77.5946m,
                AccuracyMeters = 10m,
                RecordedAt = DateTime.UtcNow
            };

            // Act
            var alerts = await detector.EvaluateBreadcrumbAsync(breadcrumb);

            // Assert: No new breach event and no new alert broadcast!
            Assert.Empty(alerts);
            Assert.Empty(_savedBreaches);
            _streamPublisherMock.Verify(s => s.PublishGeofenceAlertAsync(It.IsAny<GeofenceAlertDto>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task EvaluateBreadcrumbAsync_WhenTransitionFromOutsideToInside_TriggersEntryEventAndAlert()
        {
            // Arrange
            var geofence = CreateCircularGeofence(lat: 12.9716, lng: 77.5946, radius: 200.0);
            _geofenceRepoMock.Setup(r => r.GetAssignedGeofencesByEmployeeIdAsync(101))
                .ReturnsAsync(new List<Geofence> { geofence });

            // Seed prior state as OUTSIDE
            await _stateCache.SetStateAsync(101, geofence.Id, "OUTSIDE");

            var detector = new GeofenceBreachDetector(
                _geofenceRepoMock.Object,
                _breachRepoMock.Object,
                _validationService,
                _stateCache,
                _streamPublisherMock.Object);

            // Moved back inside geofence
            var breadcrumb = new LocationBreadcrumb
            {
                UserId = 101,
                Latitude = 12.9717m,
                Longitude = 77.5947m,
                AccuracyMeters = 5m,
                RecordedAt = DateTime.UtcNow
            };

            // Act
            var alerts = await detector.EvaluateBreadcrumbAsync(breadcrumb);

            // Assert
            Assert.Single(alerts);
            var alert = alerts[0];
            Assert.Equal("ENTRY", alert.BreachType);
            Assert.Equal("INFO", alert.Severity);
            Assert.Contains("entered assigned geofence", alert.Message);

            Assert.Single(_savedBreaches);
            Assert.Equal("ENTRY", _savedBreaches[0].BreachType);

            // Verify cache updated to INSIDE
            var state = await _stateCache.GetStateAsync(101, geofence.Id);
            Assert.Equal("INSIDE", state);
        }

        [Fact]
        public async Task AcknowledgeBreachCommandHandler_SuccessfullyAcknowledgesBreach()
        {
            // Arrange
            var breach = new GeofenceBreachEvent(
                101, 1, "HQ Site", "SITE-001", "EXIT",
                12.99m, 77.59m, 10m, 150.0, 25m, DateTime.UtcNow);

            var breachRepoMock = new Mock<IGeofenceBreachRepository>();
            breachRepoMock.Setup(r => r.GetByIdAsync(42, It.IsAny<CancellationToken>()))
                .ReturnsAsync(breach);
            breachRepoMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var handler = new AcknowledgeBreachCommandHandler(breachRepoMock.Object);
            var command = new AcknowledgeBreachCommand
            {
                BreachId = 42,
                AcknowledgedBy = "supervisor@ffm.com",
                Notes = "Spoke with employee, returning to site"
            };

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.True(result.IsAcknowledged);
            Assert.Equal("supervisor@ffm.com", result.AcknowledgedBy);
            Assert.Equal("Spoke with employee, returning to site", result.Notes);
            Assert.NotNull(result.AcknowledgedAt);
            breachRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task AcknowledgeBreachCommandHandler_WhenNotFound_ThrowsKeyNotFoundException()
        {
            // Arrange
            var breachRepoMock = new Mock<IGeofenceBreachRepository>();
            breachRepoMock.Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>()))
                .ReturnsAsync((GeofenceBreachEvent?)null);

            var handler = new AcknowledgeBreachCommandHandler(breachRepoMock.Object);
            var command = new AcknowledgeBreachCommand
            {
                BreachId = 999,
                AcknowledgedBy = "supervisor@ffm.com"
            };

            // Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(command, CancellationToken.None));
        }

        [Fact]
        public async Task GetGeofenceBreachesQueryHandler_ReturnsPagedResult()
        {
            // Arrange
            var breaches = new List<GeofenceBreachEvent>
            {
                new(101, 1, "Site A", "SITE-A", "EXIT", 12.99m, 77.59m, 10m, 50.0, 20m, DateTime.UtcNow.AddMinutes(-30)),
                new(101, 1, "Site A", "SITE-A", "ENTRY", 12.97m, 77.59m, 5m, 0.0, 5m, DateTime.UtcNow.AddMinutes(-10))
            };

            var breachRepoMock = new Mock<IGeofenceBreachRepository>();
            breachRepoMock.Setup(r => r.GetBreachesCountAsync(101, null, null, null, null, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(2);
            breachRepoMock.Setup(r => r.GetBreachesAsync(101, null, null, null, null, null, 1, 50, It.IsAny<CancellationToken>()))
                .ReturnsAsync(breaches);

            var handler = new GetGeofenceBreachesQueryHandler(breachRepoMock.Object);
            var query = new GetGeofenceBreachesQuery { UserId = 101, Page = 1, PageSize = 50 };

            // Act
            var result = await handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(2, result.TotalCount);
            Assert.Equal(2, result.Items.Count);
            Assert.Equal("EXIT", result.Items[0].BreachType);
            Assert.Equal("ENTRY", result.Items[1].BreachType);
        }

        [Fact]
        public async Task CreateBreadcrumbCommandHandler_TriggersBreachDetectorOnIngestion()
        {
            // Arrange
            var breadcrumbRepoMock = new Mock<IBreadcrumbRepository>();
            var breachDetectorMock = new Mock<IGeofenceBreachDetector>();
            var streamPublisherMock = new Mock<ILocationStreamPublisher>();

            breadcrumbRepoMock.Setup(r => r.AddAsync(It.IsAny<LocationBreadcrumb>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            breadcrumbRepoMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            breachDetectorMock.Setup(d => d.EvaluateBreadcrumbAsync(It.IsAny<LocationBreadcrumb>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<GeofenceAlertDto>());

            var handler = new CreateBreadcrumbCommandHandler(
                breadcrumbRepoMock.Object,
                streamPublisherMock.Object,
                breachDetectorMock.Object,
                null);

            var command = new CreateBreadcrumbCommand
            {
                UserId = 101,
                Latitude = 12.9716m,
                Longitude = 77.5946m,
                AccuracyMeters = 10m,
                RecordedAt = DateTime.UtcNow
            };

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            breadcrumbRepoMock.Verify(r => r.AddAsync(It.IsAny<LocationBreadcrumb>(), It.IsAny<CancellationToken>()), Times.Once);
            breachDetectorMock.Verify(d => d.EvaluateBreadcrumbAsync(
                It.Is<LocationBreadcrumb>(b => b.UserId == 101 && b.Latitude == 12.9716m),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
