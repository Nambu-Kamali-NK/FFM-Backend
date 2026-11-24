namespace FFM.Notification.Application.DTOs;

public class RegisterDeviceRequest
{
    public string DeviceId { get; set; } = default!;
    public string Platform { get; set; } = default!; // "ANDROID", "IOS", "WEB"
    public string DeviceModel { get; set; } = default!;
    public string OsVersion { get; set; } = default!;
    public string AppVersion { get; set; } = default!;
    public string FcmToken { get; set; } = default!;
}

public class DeregisterDeviceRequest
{
    public string DeviceId { get; set; } = default!;
}

public class DeviceRegistrationResponse
{
    public int DeviceRegistrationId { get; set; }
    public int EmployeeId { get; set; }
    public string DeviceId { get; set; } = default!;
    public string Platform { get; set; } = default!;
    public string DeviceModel { get; set; } = default!;
    public string OsVersion { get; set; } = default!;
    public string AppVersion { get; set; } = default!;
    public bool IsActive { get; set; }
    public DateTime RegisteredAt { get; set; }
    public DateTime LastActiveAt { get; set; }
}

