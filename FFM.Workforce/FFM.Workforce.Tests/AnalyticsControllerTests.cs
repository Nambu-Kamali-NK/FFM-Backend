using System;
using System.Collections.Generic;
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

public class AnalyticsControllerTests
{
    private WorkforceDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<WorkforceDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new WorkforceDbContext(options);

        // Seed 2 employees
        context.Employees.Add(new Employee(
            id: 10,
            employeeCode: "MCT-TEC-010",
            userId: 10,
            name: "Nambu Kamali",
            roleId: 6,
            orgUnitId: Guid.NewGuid(),
            managerId: null,
            email: "nambu@ffm.om",
            mobile: "+919876543210",
            createdBy: "SYSTEM",
            status: EmployeeStatus.ACTIVE
        ));

        context.Employees.Add(new Employee(
            id: 11,
            employeeCode: "MCT-TEC-011",
            userId: 11,
            name: "Tariq Al-Balushi",
            roleId: 5,
            orgUnitId: Guid.NewGuid(),
            managerId: null,
            email: "tariq@ffm.om",
            mobile: "+96891234567",
            createdBy: "SYSTEM",
            status: EmployeeStatus.ACTIVE
        ));

        // Seed Client Site
        context.ClientSites.Add(new ClientSiteEntity
        {
            Id = 1,
            SiteCode = "SITE-001",
            Name = "Sultan Qaboos St",
            Address = "Al Khuwair",
            Latitude = 23.60m,
            Longitude = 58.40m,
            IsActive = true
        });

        // Seed 3 Tasks with different statuses
        context.Tasks.Add(new FieldTaskEntity
        {
            Id = 101,
            TaskNumber = "TSK-2026-001",
            Title = "Transformer Repair",
            Status = "COMPLETED",
            Priority = "HIGH",
            ClientSiteId = 1,
            AssignedEmployeeId = 10,
            CreatedAt = DateTime.UtcNow
        });

        context.Tasks.Add(new FieldTaskEntity
        {
            Id = 102,
            TaskNumber = "TSK-2026-002",
            Title = "Valve Overhaul",
            Status = "EN_ROUTE",
            Priority = "MEDIUM",
            ClientSiteId = 1,
            AssignedEmployeeId = 10,
            CreatedAt = DateTime.UtcNow
        });

        context.Tasks.Add(new FieldTaskEntity
        {
            Id = 103,
            TaskNumber = "TSK-2026-003",
            Title = "Streetlight Inspection",
            Status = "ASSIGNED",
            Priority = "LOW",
            ClientSiteId = 1,
            AssignedEmployeeId = 11,
            CreatedAt = DateTime.UtcNow
        });

        // Seed Visits
        context.Visits.Add(new VisitEntity
        {
            Id = 501,
            TaskId = 101,
            EmployeeId = 10,
            CheckInTime = DateTime.UtcNow.AddMinutes(-60),
            CheckOutTime = DateTime.UtcNow,
            DurationMinutes = 60m,
            CustomerRating = 5,
            IsWithinGeofence = true,
            Status = "COMPLETED",
            CompletionNotes = "Fixed transformer"
        });

        context.SaveChanges();
        return context;
    }

    private static JsonElement ToJson(object? obj)
    {
        var json = JsonSerializer.Serialize(obj);
        return JsonDocument.Parse(json).RootElement;
    }

    [Fact]
    public async Task GetDashboardSummary_ReturnsSuccessWithKpisAndMetrics()
    {
        var db = GetInMemoryDbContext();
        var controller = new AnalyticsController(db);

        var result = await controller.GetDashboardSummary(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var root = ToJson(okResult.Value);

        Assert.True(root.GetProperty("success").GetBoolean());
        var data = root.GetProperty("data");

        // Verify KPIs
        var kpis = data.GetProperty("kpis");
        Assert.Equal(2, kpis.GetProperty("totalEmployees").GetInt32());
        Assert.Equal(2, kpis.GetProperty("activeEmployees").GetInt32());
        Assert.Equal(3, kpis.GetProperty("totalTasks").GetInt32());
        Assert.Equal(1, kpis.GetProperty("completedTasks").GetInt32());

        // Verify Task Metrics
        var taskMetrics = data.GetProperty("taskMetrics");
        Assert.Equal(3, taskMetrics.GetProperty("total").GetInt32());
        Assert.Equal(1, taskMetrics.GetProperty("completed").GetInt32());
        Assert.Equal(1, taskMetrics.GetProperty("inProgress").GetInt32());
        Assert.Equal(1, taskMetrics.GetProperty("pending").GetInt32());

        // Verify Directorate stats array
        var directorates = data.GetProperty("directorateStats");
        Assert.True(directorates.GetArrayLength() > 0);
    }

    [Fact]
    public async Task GetTaskTrends_WeekView_Returns5Days()
    {
        var db = GetInMemoryDbContext();
        var controller = new AnalyticsController(db);

        var result = await controller.GetTaskTrends("week", CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var root = ToJson(okResult.Value);

        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal("week", root.GetProperty("view").GetString());
        var items = root.GetProperty("data");
        Assert.Equal(5, items.GetArrayLength());
    }

    [Fact]
    public async Task GetTaskTrends_MonthView_Returns4Weeks()
    {
        var db = GetInMemoryDbContext();
        var controller = new AnalyticsController(db);

        var result = await controller.GetTaskTrends("month", CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var root = ToJson(okResult.Value);

        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal("month", root.GetProperty("view").GetString());
        var items = root.GetProperty("data");
        Assert.Equal(4, items.GetArrayLength());
    }

    [Fact]
    public async Task GetTechnicianLeaderboard_ReturnsRankedTechnicians()
    {
        var db = GetInMemoryDbContext();
        var controller = new AnalyticsController(db);

        var result = await controller.GetTechnicianLeaderboard(10, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var root = ToJson(okResult.Value);

        Assert.True(root.GetProperty("success").GetBoolean());
        var items = root.GetProperty("data");
        Assert.True(items.GetArrayLength() >= 2);

        // Top technician should be employee 10 (completed 1 task)
        var topTech = items[0];
        Assert.Equal(10, topTech.GetProperty("employeeId").GetInt32());
        Assert.Equal(1, topTech.GetProperty("completedTasks").GetInt32());
    }

    [Fact]
    public async Task GetLiveActivities_ReturnsRecentEvents()
    {
        var db = GetInMemoryDbContext();
        var controller = new AnalyticsController(db);

        var result = await controller.GetLiveActivities(10, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var root = ToJson(okResult.Value);

        Assert.True(root.GetProperty("success").GetBoolean());
        var items = root.GetProperty("data");
        Assert.True(items.GetArrayLength() > 0);
    }

    [Fact]
    public async Task GetGeofenceCompliance_ReturnsComplianceMetrics()
    {
        var db = GetInMemoryDbContext();
        var controller = new AnalyticsController(db);

        var result = await controller.GetGeofenceCompliance(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var root = ToJson(okResult.Value);

        Assert.True(root.GetProperty("success").GetBoolean());
        var data = root.GetProperty("data");
        Assert.Equal(1, data.GetProperty("totalVisits").GetInt32());
        Assert.Equal(1, data.GetProperty("verifiedVisits").GetInt32());
        Assert.Equal(100.0, data.GetProperty("compliancePercentage").GetDouble());
    }
}
