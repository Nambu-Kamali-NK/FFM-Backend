using System;
using System.Text.Json;
using FFM.Workforce.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FFM.Workforce.Tests;

public class NotificationsControllerTests
{
    private readonly NotificationsController _controller;

    public NotificationsControllerTests()
    {
        _controller = new NotificationsController();
    }

    private static JsonElement ToJson(object value)
    {
        var json = JsonSerializer.Serialize(value);
        return JsonDocument.Parse(json).RootElement;
    }

    [Fact]
    public void GetTemplates_ReturnsSuccessWithTemplatesList()
    {
        // Act
        var result = _controller.GetTemplates(null);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = ToJson(okResult.Value!);
        Assert.True(json.GetProperty("success").GetBoolean());
        Assert.True(json.GetProperty("total").GetInt32() > 0);
        Assert.True(json.GetProperty("data").GetArrayLength() > 0);
    }

    [Fact]
    public void GetTemplates_WithQuery_FiltersTemplates()
    {
        // Act
        var result = _controller.GetTemplates("Emergency");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = ToJson(okResult.Value!);
        Assert.True(json.GetProperty("success").GetBoolean());
        Assert.True(json.GetProperty("total").GetInt32() >= 1);
    }

    [Fact]
    public void SendNotification_WithValidRequest_ReturnsDeliveredReceipt()
    {
        // Arrange
        var request = new SendNotificationRequest
        {
            Title = "Water Outage Warning",
            Message = "Valve replacement underway in Seeb.",
            Priority = "HIGH",
            TargetType = "ALL_FIELD_TECH",
            Channels = new[] { "PUSH", "SMS" }
        };

        // Act
        var result = _controller.SendNotification(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = ToJson(okResult.Value!);
        Assert.True(json.GetProperty("success").GetBoolean());
        Assert.Equal("DELIVERED", json.GetProperty("status").GetString());
        Assert.StartsWith("NOTIF-", json.GetProperty("messageId").GetString()!);
        Assert.True(json.GetProperty("recipientCount").GetInt32() > 0);
    }

    [Fact]
    public void SendNotification_WithMissingFields_ReturnsBadRequest()
    {
        // Arrange
        var request = new SendNotificationRequest
        {
            Title = "",
            Message = ""
        };

        // Act
        var result = _controller.SendNotification(request);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var json = ToJson(badRequest.Value!);
        Assert.False(json.GetProperty("success").GetBoolean());
    }

    [Fact]
    public void GetHistory_ReturnsRecentBroadcasts()
    {
        // Act
        var result = _controller.GetHistory(priority: null, limit: 10);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = ToJson(okResult.Value!);
        Assert.True(json.GetProperty("success").GetBoolean());
        Assert.True(json.GetProperty("total").GetInt32() > 0);
    }

    [Fact]
    public void GetActiveDevices_ReturnsRegisteredDevicesList()
    {
        // Act
        var result = _controller.GetActiveDevices();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = ToJson(okResult.Value!);
        Assert.True(json.GetProperty("success").GetBoolean());
        Assert.True(json.GetProperty("data").GetArrayLength() > 0);
    }

    [Fact]
    public void RegisterDevice_WithValidToken_ReturnsSuccess()
    {
        // Arrange
        var request = new RegisterDeviceRequest
        {
            DeviceId = "DEV-TEST-001",
            Token = "fcm_token_sample_xyz",
            Platform = "Android",
            EmployeeCode = "MCT-TEC-010"
        };

        // Act
        var result = _controller.RegisterDevice(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = ToJson(okResult.Value!);
        Assert.True(json.GetProperty("success").GetBoolean());
        Assert.True(json.GetProperty("registered").GetBoolean());
    }
}
