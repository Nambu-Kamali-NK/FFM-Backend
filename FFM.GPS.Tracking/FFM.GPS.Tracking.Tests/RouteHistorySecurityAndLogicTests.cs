using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FFM.GPS.Tracking.Api.Controllers;
using FFM.GPS.Tracking.Application.Commands;
using FFM.GPS.Tracking.Application.DTOs;
using FFM.GPS.Tracking.Application.Queries;
using FFM.GPS.Tracking.Application.Services;
using FFM.GPS.Tracking.Domain.Entities;
using FFM.GPS.Tracking.Domain.Interfaces;
using FFM.GPS.Tracking.Domain.Services;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FFM.GPS.Tracking.Tests;

public class RouteHistorySecurityAndLogicTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly TrackingController _controller;

    public RouteHistorySecurityAndLogicTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _controller = new TrackingController(_mediatorMock.Object);
    }

    private void SetUserContext(string userId, string role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new("userId", userId),
            new("sub", userId),
            new(ClaimTypes.Role, role),
            new("role", role)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    #region Security Tests (RBAC & IDOR)

    [Fact]
    public async Task Security_IDOR_EmployeeCannotViewAnotherEmployeeRoute_Returns403Forbidden()
    {
        // Arrange: Logged in as User 202
        SetUserContext(userId: "202", role: "EMPLOYEE");

        // Act: Attempt to access User 468's route
        var result = await _controller.GetTravelRouteHistory(userId: 468, date: "2026-10-12");

        // Assert: Must be rejected with 403 Forbidden
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);

        var apiResponse = Assert.IsType<ApiResponse<object>>(objectResult.Value);
        Assert.False(apiResponse.Success);
        Assert.Equal("FORBIDDEN_EMPLOYEE_ACCESS", apiResponse.Error?.Code);
    }

    [Fact]
    public async Task Security_IDOR_EmployeeCannotRecordBreadcrumbForAnotherEmployee_Returns403Forbidden()
    {
        // Arrange: Logged in as User 202
        SetUserContext(userId: "202", role: "EMPLOYEE");

        var request = new CreateBreadcrumbRequest
        {
            UserId = 999, // Attempt to spoof breadcrumb for User 999
            Latitude = 13.0827m,
            Longitude = 80.2707m,
            AccuracyMeters = 5.0m,
            RecordedAt = DateTime.UtcNow
        };

        // Act
        var result = await _controller.RecordBreadcrumb(request);

        // Assert: Must be rejected with 403 Forbidden
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);

        var apiResponse = Assert.IsType<ApiResponse<object>>(objectResult.Value);
        Assert.False(apiResponse.Success);
        Assert.Equal("FORBIDDEN_EMPLOYEE_ACCESS", apiResponse.Error?.Code);
    }

    [Fact]
    public async Task Security_EmployeeCanRecordBreadcrumbForSelf_Returns201Created()
    {
        // Arrange: Logged in as User 202
        SetUserContext(userId: "202", role: "EMPLOYEE");

        _mediatorMock.Setup(m => m.Send(It.IsAny<CreateBreadcrumbCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BreadcrumbResponseDto { BreadcrumbId = 1, UserId = 202, RecordedAt = DateTime.UtcNow });

        var request = new CreateBreadcrumbRequest
        {
            UserId = 202, // Own ID
            Latitude = 13.0827m,
            Longitude = 80.2707m,
            AccuracyMeters = 5.0m,
            RecordedAt = DateTime.UtcNow
        };

        // Act
        var result = await _controller.RecordBreadcrumb(request);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, statusResult.StatusCode);
    }

    [Fact]
    public async Task Security_EmployeeCanViewOwnRoute_Returns200OK()
    {
        // Arrange: Logged in as User 202
        SetUserContext(userId: "202", role: "EMPLOYEE");

        _mediatorMock.Setup(m => m.Send(It.IsAny<GetTravelRouteHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelRouteHistoryResponseDto { UserId = 202, Date = "2026-10-12" });

        // Act: Access own route (202)
        var result = await _controller.GetTravelRouteHistory(userId: 202, date: "2026-10-12");

        // Assert: Approved with 200 OK
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
    }

    [Fact]
    public async Task Security_ManagerCanViewAnyEmployeeRoute_Returns200OK()
    {
        // Arrange: Logged in as Manager 501
        SetUserContext(userId: "501", role: "MANAGER");

        _mediatorMock.Setup(m => m.Send(It.IsAny<GetTravelRouteHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelRouteHistoryResponseDto { UserId = 468, Date = "2026-10-12" });

        // Act: Manager accesses User 468's route
        var result = await _controller.GetTravelRouteHistory(userId: 468, date: "2026-10-12");

        // Assert: Approved with 200 OK
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
    }

    [Fact]
    public async Task Security_AdminCanViewAnyEmployeeRoute_Returns200OK()
    {
        // Arrange: Logged in as Admin 10
        SetUserContext(userId: "10", role: "ADMIN");

        _mediatorMock.Setup(m => m.Send(It.IsAny<GetTravelRouteHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelRouteHistoryResponseDto { UserId = 468, Date = "2026-10-12" });

        // Act: Admin accesses User 468's route
        var result = await _controller.GetTravelRouteHistory(userId: 468, date: "2026-10-12");

        // Assert: Approved with 200 OK
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
    }

    [Fact]
    public async Task Security_SqlInjectionDatePayload_Returns400BadRequest()
    {
        SetUserContext(userId: "10", role: "ADMIN");

        // Act: Send SQL Injection payload in date
        var result = await _controller.GetTravelRouteHistory(userId: 468, date: "' OR '1'='1; DROP TABLE--");

        // Assert: Blocked with 400 Bad Request
        _mediatorMock.Setup(m => m.Send(It.IsAny<GetTravelRouteHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("The date must be supplied in ISO format 'YYYY-MM-DD'."));

        var badRequest = Assert.IsType<BadRequestObjectResult>(await _controller.GetTravelRouteHistory(468, "' OR 1=1--"));
        var resp = Assert.IsType<ApiResponse<object>>(badRequest.Value);
        Assert.False(resp.Success);
    }

    [Fact]
    public async Task Security_MalformedDateThrowsFormatException_Returns400BadRequest()
    {
        SetUserContext(userId: "10", role: "ADMIN");

        _mediatorMock.Setup(m => m.Send(It.IsAny<GetTravelRouteHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FormatException("String '2026-02-31' was not recognized as a valid DateTime."));

        var badRequest = Assert.IsType<BadRequestObjectResult>(await _controller.GetTravelRouteHistory(468, "2026-02-31"));
        var resp = Assert.IsType<ApiResponse<object>>(badRequest.Value);
        Assert.False(resp.Success);
        Assert.Equal("INVALID_DATE_FORMAT", resp.Error?.Code);
    }

    [Fact]
    public async Task Security_InvalidEmployeeId_Returns400BadRequest()
    {
        SetUserContext(userId: "10", role: "ADMIN");

        var result = await _controller.GetTravelRouteHistory(userId: -5, date: "2026-10-12");
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var resp = Assert.IsType<ApiResponse<object>>(badRequest.Value);
        Assert.Equal("INVALID_USER_ID", resp.Error?.Code);
    }

    #endregion

    #region Mathematical & Analytics Engine Tests

    [Fact]
    public void AnalyticsEngine_CalculatesHaversineAndSuppressesJitter()
    {
        var validationService = new GeofenceValidationService();
        var analytics = new RouteAnalyticsService(validationService);

        var now = new DateTime(2026, 10, 12, 9, 0, 0, DateTimeKind.Utc);
        var points = new List<LocationBreadcrumb>
        {
            // Point 1: Origin
            new() { UserId = 468, Latitude = 13.0827m, Longitude = 80.2707m, SpeedKmh = 0m, RecordedAt = now },
            // Point 2: Moved 5km away at 40 km/h (Valid movement)
            new() { UserId = 468, Latitude = 13.0450m, Longitude = 80.2500m, SpeedKmh = 40m, RecordedAt = now.AddMinutes(15) },
            // Point 3: Micro-movement (drifts 5 meters while stationary at 0 km/h) -> JITTER!
            new() { UserId = 468, Latitude = 13.04504m, Longitude = 80.25004m, SpeedKmh = 0m, RecordedAt = now.AddMinutes(25) }
        };

        var result = analytics.CalculateRouteHistory(468, "2026-10-12", points, new List<Geofence>());

        Assert.NotNull(result);
        Assert.True(result.Metrics.TotalDistanceKilometers > 4.0 && result.Metrics.TotalDistanceKilometers < 6.0);
        Assert.Equal(40.0, result.Metrics.MaxSpeedKmh);
        Assert.Equal(25, result.Metrics.TotalDurationMinutes);
        Assert.Equal(3, result.Polyline.Count);
    }

    [Fact]
    public void AnalyticsEngine_DetectsStationaryStopsAccurately()
    {
        var validationService = new GeofenceValidationService();
        var analytics = new RouteAnalyticsService(validationService);

        var now = new DateTime(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc);
        var geofence = new Geofence(
            "LOC_1",
            "Guindy Tech Center",
            "CIRCULAR",
            13.0100,
            80.2100,
            100.0,
            15.0);

        var points = new List<LocationBreadcrumb>
        {
            // 3 pings inside the same 20m circle across 20 minutes
            new() { UserId = 468, Latitude = 13.0100m, Longitude = 80.2100m, SpeedKmh = 0m, RecordedAt = now },
            new() { UserId = 468, Latitude = 13.0101m, Longitude = 80.2101m, SpeedKmh = 0m, RecordedAt = now.AddMinutes(10) },
            new() { UserId = 468, Latitude = 13.0100m, Longitude = 80.2100m, SpeedKmh = 0m, RecordedAt = now.AddMinutes(20) }
        };

        var result = analytics.CalculateRouteHistory(468, "2026-10-12", points, new List<Geofence> { geofence }, minDwellMinutes: 5);

        Assert.Single(result.Stops);
        Assert.Equal("Guindy Tech Center", result.Stops[0].SiteName);
        Assert.Equal(20, result.Stops[0].DwellDurationMinutes);
        Assert.Equal("GEOFENCE_SITE", result.Stops[0].StopType);
    }

    [Fact]
    public void AnalyticsEngine_DetectsStopsAndMatchesPolygonGeofenceSite()
    {
        var validationService = new GeofenceValidationService();
        var analytics = new RouteAnalyticsService(validationService);

        // A square polygon around (13.0000, 80.2000)
        var polygonJson = "[{\"latitude\":12.9990,\"longitude\":80.1990},{\"latitude\":13.0010,\"longitude\":80.1990},{\"latitude\":13.0010,\"longitude\":80.2010},{\"latitude\":12.9990,\"longitude\":80.2010}]";
        var geofence = new Geofence("LOC-DLF", "DLF IT Park Campus", "POLYGON", null, null, null, 15.0, polygonJson);

        var now = DateTime.UtcNow;
        var points = new List<LocationBreadcrumb>
        {
            new() { UserId = 468, Latitude = 13.0000m, Longitude = 80.2000m, SpeedKmh = 0m, RecordedAt = now },
            new() { UserId = 468, Latitude = 13.0001m, Longitude = 80.2001m, SpeedKmh = 0m, RecordedAt = now.AddMinutes(15) }
        };

        var result = analytics.CalculateRouteHistory(468, "2026-10-12", points, new List<Geofence> { geofence }, minDwellMinutes: 5);

        Assert.Single(result.Stops);
        Assert.Equal("DLF IT Park Campus", result.Stops[0].SiteName);
        Assert.Equal(15, result.Stops[0].DwellDurationMinutes);
        Assert.Equal("GEOFENCE_SITE", result.Stops[0].StopType);
    }

    [Fact]
    public void AnalyticsEngine_IncludePolylineFalse_OmitsPolylineData()
    {
        var validationService = new GeofenceValidationService();
        var analytics = new RouteAnalyticsService(validationService);

        var now = DateTime.UtcNow;
        var points = new List<LocationBreadcrumb>
        {
            new() { UserId = 468, Latitude = 13.0827m, Longitude = 80.2707m, RecordedAt = now },
            new() { UserId = 468, Latitude = 13.0450m, Longitude = 80.2500m, RecordedAt = now.AddMinutes(10) }
        };

        // Act with includePolyline = false
        var result = analytics.CalculateRouteHistory(468, "2026-10-12", points, new List<Geofence>(), includePolyline: false);

        Assert.Empty(result.Polyline);
        Assert.True(result.Metrics.TotalDistanceKilometers > 0);
    }

    #endregion
}
