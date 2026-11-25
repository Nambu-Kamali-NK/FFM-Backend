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

public class ReportsControllerTests
{
    private WorkforceDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<WorkforceDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new WorkforceDbContext(options);

        // Seed Employee
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

        // Seed Task
        context.Tasks.Add(new FieldTaskEntity
        {
            Id = 101,
            TaskNumber = "TSK-2026-001",
            Title = "Pump Station Overhaul",
            Status = "COMPLETED",
            Priority = "HIGH",
            ClientSiteId = 1,
            AssignedEmployeeId = 10,
            ScheduledStart = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        });

        // Seed Visit with customer signature
        context.Visits.Add(new VisitEntity
        {
            Id = 501,
            TaskId = 101,
            EmployeeId = 10,
            CheckInTime = DateTime.UtcNow.AddDays(-1),
            CheckOutTime = DateTime.UtcNow.AddDays(-1).AddHours(1),
            DurationMinutes = 60m,
            CustomerName = "Eng. Said Al-Habsi",
            CustomerSignatureUrl = "https://s3.amazonaws.com/sig.png",
            CustomerRating = 5,
            Status = "COMPLETED",
            CompletionNotes = "Pump replaced successfully"
        });

        // Seed Attendance Record
        context.AttendanceRecords.Add(new AttendanceRecord(
            employeeId: 10,
            checkInTime: DateTime.UtcNow.AddHours(-8),
            checkInNotes: "Morning shift"
        ));

        context.SaveChanges();
        return context;
    }

    private static JsonElement ToJson(object? obj)
    {
        var json = JsonSerializer.Serialize(obj);
        return JsonDocument.Parse(json).RootElement;
    }

    [Fact]
    public void GetAvailableReports_ReturnsListOfSupportedReports()
    {
        var db = GetInMemoryDbContext();
        var controller = new ReportsController(db);

        var result = controller.GetAvailableReports();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var root = ToJson(okResult.Value);

        Assert.True(root.GetProperty("success").GetBoolean());
        var reports = root.GetProperty("data");
        Assert.True(reports.GetArrayLength() >= 3);
    }

    [Fact]
    public async Task ExportTasks_ExcelFormat_ReturnsExcelFileWithContent()
    {
        var db = GetInMemoryDbContext();
        var controller = new ReportsController(db);

        var request = new ReportsController.TaskExportRequest(
            StartDate: DateTime.UtcNow.AddDays(-7),
            EndDate: DateTime.UtcNow.AddDays(1),
            Status: "COMPLETED",
            Format: "EXCEL",
            AssignedEmployeeId: null,
            ClientSiteId: null);

        var result = await controller.ExportTasks(request, CancellationToken.None);

        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/vnd.ms-excel", fileResult.ContentType);
        Assert.Contains(".xls", fileResult.FileDownloadName);
        Assert.True(fileResult.FileContents.Length > 0);
    }

    [Fact]
    public async Task ExportTasks_PdfFormat_ReturnsPdfFileWithContent()
    {
        var db = GetInMemoryDbContext();
        var controller = new ReportsController(db);

        var request = new ReportsController.TaskExportRequest(
            StartDate: null,
            EndDate: null,
            Status: "ALL",
            Format: "PDF",
            AssignedEmployeeId: null,
            ClientSiteId: null);

        var result = await controller.ExportTasks(request, CancellationToken.None);

        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", fileResult.ContentType);
        Assert.Contains(".pdf", fileResult.FileDownloadName);
        Assert.True(fileResult.FileContents.Length > 0);

        // Verify PDF Header %PDF-1.4
        var header = System.Text.Encoding.ASCII.GetString(fileResult.FileContents, 0, 8);
        Assert.StartsWith("%PDF-1.4", header);
    }

    [Fact]
    public async Task ExportTasks_CsvFormat_ReturnsCsvFileWithBom()
    {
        var db = GetInMemoryDbContext();
        var controller = new ReportsController(db);

        var request = new ReportsController.TaskExportRequest(
            StartDate: null,
            EndDate: null,
            Status: null,
            Format: "CSV",
            AssignedEmployeeId: null,
            ClientSiteId: null);

        var result = await controller.ExportTasks(request, CancellationToken.None);

        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("text/csv; charset=utf-8", fileResult.ContentType);
        Assert.Contains(".csv", fileResult.FileDownloadName);
        Assert.True(fileResult.FileContents.Length > 3);

        // Verify UTF-8 BOM: 0xEF, 0xBB, 0xBF
        Assert.Equal(0xEF, fileResult.FileContents[0]);
        Assert.Equal(0xBB, fileResult.FileContents[1]);
        Assert.Equal(0xBF, fileResult.FileContents[2]);
    }

    [Fact]
    public async Task ExportAttendance_ExcelFormat_ReturnsFileContentResult()
    {
        var db = GetInMemoryDbContext();
        var controller = new ReportsController(db);

        var request = new ReportsController.AttendanceExportRequest(
            StartDate: DateTime.UtcNow.AddDays(-30),
            EndDate: DateTime.UtcNow.AddDays(1),
            Format: "EXCEL",
            EmployeeId: 10);

        var result = await controller.ExportAttendance(request, CancellationToken.None);

        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/vnd.ms-excel", fileResult.ContentType);
        Assert.Contains(".xls", fileResult.FileDownloadName);
        Assert.True(fileResult.FileContents.Length > 0);
    }

    [Fact]
    public async Task ExportAttendance_PdfFormat_ReturnsValidPdf()
    {
        var db = GetInMemoryDbContext();
        var controller = new ReportsController(db);

        var request = new ReportsController.AttendanceExportRequest(
            StartDate: null,
            EndDate: null,
            Format: "PDF",
            EmployeeId: null);

        var result = await controller.ExportAttendance(request, CancellationToken.None);

        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", fileResult.ContentType);
        Assert.True(fileResult.FileContents.Length > 0);

        var header = System.Text.Encoding.ASCII.GetString(fileResult.FileContents, 0, 8);
        Assert.StartsWith("%PDF-1.4", header);
    }

    [Fact]
    public async Task ExportTechnicianSummary_PdfFormat_ReturnsValidPdf()
    {
        var db = GetInMemoryDbContext();
        var controller = new ReportsController(db);

        var request = new ReportsController.TechnicianSummaryExportRequest(
            EmployeeId: 10,
            Format: "PDF");

        var result = await controller.ExportTechnicianSummary(request, CancellationToken.None);

        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", fileResult.ContentType);
        Assert.Contains(".pdf", fileResult.FileDownloadName);
        Assert.True(fileResult.FileContents.Length > 0);

        var header = System.Text.Encoding.ASCII.GetString(fileResult.FileContents, 0, 8);
        Assert.StartsWith("%PDF-1.4", header);
    }
}
