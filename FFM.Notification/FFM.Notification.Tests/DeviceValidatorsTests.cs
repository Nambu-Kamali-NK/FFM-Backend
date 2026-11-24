using FFM.Notification.Application.Commands;
using FFM.Notification.Application.Validators;
using Xunit;

namespace FFM.Notification.Tests;

public class DeviceValidatorsTests
{
    private readonly RegisterDeviceCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidAndroidCommand_PassesValidation()
    {
        var command = new RegisterDeviceCommand(
            EmployeeId: 10,
            DeviceId: "device-valid-123",
            Platform: "ANDROID",
            DeviceModel: "Samsung Galaxy A54",
            OsVersion: "Android 13",
            AppVersion: "2.1.0",
            FcmToken: new string('f', 150)
        );

        var result = _validator.Validate(command);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ValidIosCommand_PassesValidation()
    {
        var command = new RegisterDeviceCommand(
            EmployeeId: 10,
            DeviceId: "apple-idfv-456",
            Platform: "IOS",
            DeviceModel: "iPhone 15 Pro",
            OsVersion: "iOS 17.4",
            AppVersion: "2.1.0",
            FcmToken: new string('i', 150)
        );

        var result = _validator.Validate(command);
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("WINDOWS_PHONE")]
    [InlineData("SYMBIAN")]
    [InlineData("BLACKBERRY")]
    [InlineData("")]
    public void Validate_UnsupportedPlatform_FailsValidation(string platform)
    {
        var command = new RegisterDeviceCommand(
            EmployeeId: 10,
            DeviceId: "device-bad-platform",
            Platform: platform,
            DeviceModel: "Nokia Lumia",
            OsVersion: "WP 8.1",
            AppVersion: "1.0.0",
            FcmToken: new string('t', 100)
        );

        var result = _validator.Validate(command);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Platform");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_EmptyDeviceId_FailsValidation(string? deviceId)
    {
        var command = new RegisterDeviceCommand(
            EmployeeId: 10,
            DeviceId: deviceId!,
            Platform: "ANDROID",
            DeviceModel: "Pixel 7",
            OsVersion: "Android 14",
            AppVersion: "1.0.0",
            FcmToken: new string('t', 100)
        );

        var result = _validator.Validate(command);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "DeviceId");
    }

    [Fact]
    public void Validate_ShortFcmToken_FailsValidation()
    {
        var command = new RegisterDeviceCommand(
            EmployeeId: 10,
            DeviceId: "device-short-token",
            Platform: "ANDROID",
            DeviceModel: "Pixel 7",
            OsVersion: "Android 14",
            AppVersion: "1.0.0",
            FcmToken: "too-short-token-under-50-chars"
        );

        var result = _validator.Validate(command);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "FcmToken");
    }

    [Fact]
    public void Validate_ZeroOrNegativeEmployeeId_FailsValidation()
    {
        var command = new RegisterDeviceCommand(
            EmployeeId: 0,
            DeviceId: "device-valid-id",
            Platform: "ANDROID",
            DeviceModel: "Pixel 7",
            OsVersion: "Android 14",
            AppVersion: "1.0.0",
            FcmToken: new string('a', 100)
        );

        var result = _validator.Validate(command);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "EmployeeId");
    }
}

