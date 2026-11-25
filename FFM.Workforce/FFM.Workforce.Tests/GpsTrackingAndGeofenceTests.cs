using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FFM.Workforce.Api.Controllers;
using FFM.Workforce.Domain.Entities;
using FFM.Workforce.Infrastructure;
using Xunit;

namespace FFM.Workforce.Tests;

public class GpsTrackingAndGeofenceTests
{
    private static JsonElement ToJson(object? obj)
    {
        var json = JsonSerializer.Serialize(obj);
        return JsonDocument.Parse(json).RootElement;
    }
    private WorkforceDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<WorkforceDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new WorkforceDbContext(options);

        // Seed Client Sites
        context.ClientSites.AddRange(
            new ClientSiteEntity
            {
                Id = 1,
                SiteCode = "SITE-001",
                Name = "Sultan Qaboos St - Al Khuwair",
                Address = "Al Khuwair, Muscat",
                Latitude = 23.6000m,
                Longitude = 58.4000m,
                RadiusMeters = 150,
                IsActive = true
            },
            new ClientSiteEntity
            {
                Id = 2,
                SiteCode = "SITE-002",
                Name = "Central Municipal Reservoir - Bawshar",
                Address = "Bawshar, Muscat",
                Latitude = 23.5800m,
                Longitude = 58.3900m,
                RadiusMeters = 200,
                IsActive = true
            }
        );

        // Seed Employees
        context.Employees.Add(new Employee(
            id: 10,
            employeeCode: "MCT-TEC-010",
            userId: 10,
            name: "Nambu Kamali",
            roleId: 1,
            orgUnitId: Guid.NewGuid(),
            managerId: null,
            email: "nambu@ffm.om",
            mobile: "+919876543210",
            createdBy: "SYSTEM",
            status: EmployeeStatus.ACTIVE
        ));

        context.SaveChanges();
        return context;
    }

    [Fact]
    public async Task GetGeofences_ReturnsActiveMunicipalGeofences()
    {
        using var context = CreateInMemoryDbContext();
        var controller = new GeofencesController(context);

        var result = await controller.GetGeofences(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task ValidateGeofence_WhenWithinAllowedRadius_ReturnsCompliantStatus()
    {
        using var context = CreateInMemoryDbContext();
        var controller = new GeofencesController(context);

        // Coordinates very close to Site-001 (23.6001, 58.4001) - ~15m away
        var request = new ValidateGeofenceDto
        {
            SiteId = 1,
            EmployeeId = 10,
            Latitude = 23.6001m,
            Longitude = 58.4001m,
            GpsAccuracyMeters = 10.0
        };

        var result = await controller.ValidateGeofence(request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var root = ToJson(okResult.Value);
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.True(root.GetProperty("data").GetProperty("isWithinGeofence").GetBoolean());
        Assert.Equal("COMPLIANT", root.GetProperty("data").GetProperty("status").GetString());
    }

    [Fact]
    public async Task ValidateGeofence_WhenOutOfBounds_ReturnsBreachStatus()
    {
        using var context = CreateInMemoryDbContext();
        var controller = new GeofencesController(context);

        // Coordinates ~10km away from Muscat sites
        var request = new ValidateGeofenceDto
        {
            SiteId = 1,
            EmployeeId = 10,
            Latitude = 23.7000m,
            Longitude = 58.6000m,
            GpsAccuracyMeters = 10.0
        };

        var result = await controller.ValidateGeofence(request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var root = ToJson(okResult.Value);
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.False(root.GetProperty("data").GetProperty("isWithinGeofence").GetBoolean());
        Assert.Equal("BREACH", root.GetProperty("data").GetProperty("status").GetString());
    }

    [Fact]
    public async Task ValidateGeofence_WhenInvalidCoordinates_ReturnsBadRequest()
    {
        using var context = CreateInMemoryDbContext();
        var controller = new GeofencesController(context);

        var request = new ValidateGeofenceDto
        {
            Latitude = 120.0m, // Invalid latitude (> 90)
            Longitude = 58.4000m
        };

        var result = await controller.ValidateGeofence(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void RecordBreadcrumb_WithValidPing_ReturnsCreatedStatus()
    {
        using var context = CreateInMemoryDbContext();
        var controller = new TrackingController(context);

        var request = new RecordBreadcrumbDto
        {
            EmployeeId = 10,
            Latitude = 23.6000m,
            Longitude = 58.4000m,
            SpeedKmh = 35.5m,
            BatteryPercentage = 90,
            ActivityType = "IN_VEHICLE"
        };

        var result = controller.RecordBreadcrumb(request);

        var createdResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, createdResult.StatusCode);
    }

    [Fact]
    public async Task GetLiveLocations_ReturnsActiveFleetPositions()
    {
        using var context = CreateInMemoryDbContext();
        var controller = new TrackingController(context);

        var result = await controller.GetLiveLocations(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var root = ToJson(okResult.Value);
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.True(root.GetProperty("data").GetProperty("fleetCount").GetInt32() > 0);
    }

    [Fact]
    public void GetTravelHistory_ReturnsRouteWaypointsAndDistance()
    {
        using var context = CreateInMemoryDbContext();
        var controller = new TrackingController(context);

        var result = controller.GetTravelHistory(employeeId: 10, date: "2026-11-25");

        var okResult = Assert.IsType<OkObjectResult>(result);
        var root = ToJson(okResult.Value);
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.True(root.GetProperty("data").GetProperty("totalDistanceKm").GetDouble() > 0);
        Assert.True(root.GetProperty("data").GetProperty("waypoints").GetArrayLength() > 0);
    }
}
