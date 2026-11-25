using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FFM.Workforce.Api.Controllers;
using FFM.Workforce.Domain.Entities;
using FFM.Workforce.Infrastructure;
using Xunit;

namespace FFM.Workforce.Tests;

public class TaskEvidenceAndSignatureTests
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

        // Seed an active task
        context.Tasks.Add(new FieldTaskEntity
        {
            Id = 101,
            TaskNumber = "TSK-2026-001",
            Title = "Solar Inverter Maintenance",
            Priority = "HIGH",
            Status = "ASSIGNED",
            ClientSiteId = 1,
            AssignedEmployeeId = 10,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
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
    public async Task VisitsController_SiteCheckIn_SucceedsAndCreatesVisit()
    {
        var db = GetInMemoryDbContext();
        var controller = new VisitsController(db);

        var request = new VisitsController.VisitCheckInRequest(
            TaskId: 101,
            Latitude: 23.6012m,
            Longitude: 58.4015m,
            AccuracyMeters: 5.0m,
            IsMockLocation: false);

        var result = await controller.CheckIn(request, CancellationToken.None);

        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, objResult.StatusCode);

        var data = ToJson(objResult.Value);
        Assert.True(data.GetProperty("success").GetBoolean());
        Assert.Equal(101, data.GetProperty("data").GetProperty("taskId").GetInt32());

        var task = await db.Tasks.FindAsync(101);
        Assert.Equal("CHECKED_IN", task!.Status);
    }

    [Fact]
    public async Task VisitsController_SiteCheckIn_IdempotentReplay_ReturnsExisting()
    {
        var db = GetInMemoryDbContext();
        var controller = new VisitsController(db);

        var request = new VisitsController.VisitCheckInRequest(
            TaskId: 101,
            Latitude: 23.6012m,
            Longitude: 58.4015m,
            AccuracyMeters: 5.0m,
            IsMockLocation: false);

        // First check-in
        await controller.CheckIn(request, CancellationToken.None);

        // Second check-in (idempotent replay)
        var result = await controller.CheckIn(request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var data = ToJson(okResult.Value);
        Assert.True(data.GetProperty("success").GetBoolean());
        Assert.True(data.GetProperty("data").GetProperty("isIdempotentReplay").GetBoolean());
    }

    [Fact]
    public async Task VisitsController_AddEvidence_RecordsEvidenceSuccessfully()
    {
        var db = GetInMemoryDbContext();
        var controller = new VisitsController(db);

        var request = new VisitsController.AddVisitEvidenceRequest(
            VisitId: 1,
            TaskId: 101,
            EvidenceTypeId: 1,
            FileName: "panel_cleaning_before.jpg",
            FolderPath: "visits/1",
            FileUrls: new List<string> { "https://s3.amazonaws.com/visits/1/panel_before.jpg" },
            ThumbnailUrl: null,
            Remarks: "Dust accumulated on photovoltaic cell array",
            CapturedLatitude: 23.6012m,
            CapturedLongitude: 58.4015m);

        var result = await controller.AddEvidence(request, CancellationToken.None);

        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, objResult.StatusCode);

        var data = ToJson(objResult.Value);
        Assert.True(data.GetProperty("success").GetBoolean());
        Assert.Equal("PHOTO", data.GetProperty("data").GetProperty("evidenceType").GetString());
    }

    [Fact]
    public void VisitsController_GenerateUploadUrls_ReturnsPreSignedUrl()
    {
        var db = GetInMemoryDbContext();
        var controller = new VisitsController(db);

        var request = new VisitsController.GenerateUploadUrlsRequest(
            VisitId: 1,
            TaskId: 101,
            FileName: "inverter_reading.jpg",
            ContentType: "image/jpeg");

        var result = controller.GenerateUploadUrls(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var data = ToJson(okResult.Value);
        Assert.True(data.GetProperty("success").GetBoolean());

        var uploadUrls = data.GetProperty("data").GetProperty("uploadUrls");
        Assert.True(uploadUrls.GetArrayLength() > 0);
        Assert.Contains("inverter_reading.jpg", uploadUrls[0].GetProperty("fileName").GetString());
    }

    [Fact]
    public async Task VisitsController_SubmitCustomerSignature_RecordsSignature()
    {
        var db = GetInMemoryDbContext();
        var controller = new VisitsController(db);

        var request = new VisitsController.SubmitCustomerSignatureRequest(
            VisitId: 1,
            TaskId: 101,
            CustomerName: "Dr. Ahmed Al-Balushi",
            SignatureData: "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==",
            CustomerRating: 5,
            Remarks: "Workmanship verified and approved");

        var result = await controller.SubmitCustomerSignature(request, CancellationToken.None);

        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, objResult.StatusCode);

        var data = ToJson(objResult.Value);
        Assert.True(data.GetProperty("success").GetBoolean());
        Assert.Equal("Dr. Ahmed Al-Balushi", data.GetProperty("data").GetProperty("customerName").GetString());
        Assert.Equal(5, data.GetProperty("data").GetProperty("customerRating").GetInt32());
    }

    [Fact]
    public async Task VisitsController_CompleteTask_WithEvidenceAndSignature_Succeeds()
    {
        var db = GetInMemoryDbContext();
        var controller = new VisitsController(db);

        var request = new VisitsController.CompleteTaskRequest(
            TaskId: 101,
            VisitId: null,
            Latitude: 23.6012m,
            Longitude: 58.4015m,
            CompletionNotes: "Solar panels cleaned and inverter efficiency benchmarked at 98.4%.",
            CustomerName: "Eng. Said Al-Habsi",
            CustomerSignatureUrl: "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==",
            CustomerRating: 5,
            Evidences: new List<VisitsController.TaskCompletionEvidenceItem>
            {
                new("PHOTO", "post_cleaning.jpg", "https://s3.amazonaws.com/post_cleaning.jpg", "Array after cleaning")
            });

        var result = await controller.CompleteTask(request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var data = ToJson(okResult.Value);
        Assert.True(data.GetProperty("success").GetBoolean());
        Assert.Equal("COMPLETED", data.GetProperty("data").GetProperty("status").GetString());
        Assert.Equal(5, data.GetProperty("data").GetProperty("customerRating").GetInt32());

        var task = await db.Tasks.FindAsync(101);
        Assert.Equal("COMPLETED", task!.Status);
    }

    [Fact]
    public async Task TasksController_CompleteTask_DirectEndpoint_Succeeds()
    {
        var db = GetInMemoryDbContext();
        var controller = new TasksController(db);

        var request = new TasksController.CompleteTaskInput(
            TaskId: 101,
            VisitId: null,
            Latitude: 23.6012m,
            Longitude: 58.4015m,
            CompletionNotes: "Replaced junction box fuse and restored 415V 3-phase power.",
            CustomerName: "MCT Maintenance Director",
            CustomerSignatureUrl: "https://s3.amazonaws.com/director_sig.png",
            CustomerRating: 5,
            Evidences: new List<TasksController.TaskCompletionEvidenceInput>
            {
                new("PHOTO", "fuse_replaced.jpg", "https://s3.amazonaws.com/fuse_replaced.jpg", "New 63A ceramic fuse installed")
            });

        var result = await controller.CompleteTask(request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var data = ToJson(okResult.Value);
        Assert.True(data.GetProperty("success").GetBoolean());
        Assert.Equal("COMPLETED", data.GetProperty("data").GetProperty("status").GetString());

        var task = await db.Tasks.FindAsync(101);
        Assert.Equal("COMPLETED", task!.Status);
    }
}
