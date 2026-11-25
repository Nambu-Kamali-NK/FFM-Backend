using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FFM.Workforce.Api.Controllers;
using FFM.Workforce.Domain.Entities;
using FFM.Workforce.Infrastructure;
using Xunit;

namespace FFM.Workforce.Tests;

public class TasksControllerTests
{
    private WorkforceDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<WorkforceDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new WorkforceDbContext(options);

        // Seed an employee
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

        // Seed a client site
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

        context.SaveChanges();
        return context;
    }

    [Fact]
    public async Task CreateTask_ValidRequest_CreatesTaskSuccessfully()
    {
        var db = GetInMemoryDbContext();
        var controller = new TasksController(db);

        var request = new TasksController.CreateTaskRequest(
            Title: "Water Pipeline Pressure Audit",
            Description: "Inspect hydraulic pressure sensors",
            Priority: "HIGH",
            ClientSiteId: 1,
            AssignedEmployeeId: 10,
            ScheduledStart: DateTime.UtcNow.AddHours(2),
            ScheduledEnd: DateTime.UtcNow.AddHours(4),
            TaskType: "INSPECTION"
        );

        var actionResult = await controller.CreateTask(request, CancellationToken.None);
        var createdResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(201, createdResult.StatusCode);

        var taskInDb = await db.Tasks.FirstOrDefaultAsync(t => t.Title == "Water Pipeline Pressure Audit");
        Assert.NotNull(taskInDb);
        Assert.Equal("ASSIGNED", taskInDb.Status);
        Assert.Equal("HIGH", taskInDb.Priority);
        Assert.StartsWith("TSK-", taskInDb.TaskNumber);
    }

    [Fact]
    public async Task AssignTask_ValidTaskAndEmployee_AssignsSuccessfully()
    {
        var db = GetInMemoryDbContext();
        var controller = new TasksController(db);

        var task = new FieldTaskEntity
        {
            TaskNumber = "TSK-202611-0001",
            Title = "Unassigned Repair",
            Priority = "MEDIUM",
            Status = "CREATED",
            CreatedAt = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var request = new TasksController.AssignTaskRequest(task.Id, 10);
        var result = await controller.AssignTask(request, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(result);

        var updated = await db.Tasks.FindAsync(task.Id);
        Assert.NotNull(updated);
        Assert.Equal("ASSIGNED", updated.Status);
        Assert.Equal(10, updated.AssignedEmployeeId);
    }

    [Fact]
    public async Task UpdateTaskStatus_ValidTransition_UpdatesStatusSuccessfully()
    {
        var db = GetInMemoryDbContext();
        var controller = new TasksController(db);

        var task = new FieldTaskEntity
        {
            TaskNumber = "TSK-202611-0002",
            Title = "Streetlight Maintenance",
            Priority = "HIGH",
            Status = "ASSIGNED",
            AssignedEmployeeId = 10,
            CreatedAt = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var request = new TasksController.UpdateTaskStatusRequest(task.Id, "EN_ROUTE", "On the way to site", null, null);
        var result = await controller.UpdateTaskStatus(request, CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);

        var updated = await db.Tasks.FindAsync(task.Id);
        Assert.NotNull(updated);
        Assert.Equal("EN_ROUTE", updated.Status);
    }

    [Fact]
    public async Task CancelTask_ValidActiveTask_CancelsSuccessfully()
    {
        var db = GetInMemoryDbContext();
        var controller = new TasksController(db);

        var task = new FieldTaskEntity
        {
            TaskNumber = "TSK-202611-0003",
            Title = "Pavement Inspection",
            Priority = "LOW",
            Status = "ASSIGNED",
            AssignedEmployeeId = 10,
            CreatedAt = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var request = new TasksController.CancelTaskRequest(task.Id, "Client requested reschedule");
        var result = await controller.CancelTask(request, CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);

        var cancelled = await db.Tasks.FindAsync(task.Id);
        Assert.NotNull(cancelled);
        Assert.Equal("CANCELLED", cancelled.Status);
        Assert.Equal("Client requested reschedule", cancelled.CancellationReason);
    }

    [Theory]
    [InlineData("CHECKED_IN")]
    [InlineData("COMPLETED")]
    public async Task CancelTask_CheckedInOrCompletedTask_ReturnsBadRequest(string status)
    {
        var db = GetInMemoryDbContext();
        var controller = new TasksController(db);

        var task = new FieldTaskEntity
        {
            TaskNumber = "TSK-202611-0004",
            Title = "Active On-Site Task",
            Priority = "CRITICAL",
            Status = status,
            AssignedEmployeeId = 10,
            CreatedAt = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var request = new TasksController.CancelTaskRequest(task.Id, "Attempted cancellation");
        var result = await controller.CancelTask(request, CancellationToken.None);
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);

        // Verify status remained untouched
        var current = await db.Tasks.FindAsync(task.Id);
        Assert.NotNull(current);
        Assert.Equal(status, current.Status);
    }
}
