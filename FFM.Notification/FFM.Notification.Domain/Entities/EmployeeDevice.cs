namespace FFM.Notification.Domain.Entities;

public class EmployeeDevice
{
    public int Id { get; private set; }
    public int EmployeeId { get; private set; }
    public string DeviceId { get; private set; } = default!;
    public string Platform { get; private set; } = default!; // "ANDROID", "IOS", "WEB"
    public string DeviceModel { get; private set; } = default!;
    public string OsVersion { get; private set; } = default!;
    public string AppVersion { get; private set; } = default!;
    public string FcmToken { get; private set; } = default!;
    public bool IsActive { get; private set; }
    public DateTime RegisteredAt { get; private set; }
    public DateTime LastActiveAt { get; private set; }

    // EF Core constructor
    protected EmployeeDevice() { }

    public EmployeeDevice(
        int employeeId,
        string deviceId,
        string platform,
        string deviceModel,
        string osVersion,
        string appVersion,
        string fcmToken)
    {
        EmployeeId = employeeId;
        DeviceId = deviceId;
        Platform = platform.ToUpperInvariant();
        DeviceModel = deviceModel;
        OsVersion = osVersion;
        AppVersion = appVersion;
        FcmToken = fcmToken;
        IsActive = true;
        RegisteredAt = DateTime.UtcNow;
        LastActiveAt = DateTime.UtcNow;
    }

    public void UpdateToken(string newToken, string deviceModel, string osVersion, string appVersion)
    {
        FcmToken = newToken;
        DeviceModel = deviceModel;
        OsVersion = osVersion;
        AppVersion = appVersion;
        IsActive = true;
        LastActiveAt = DateTime.UtcNow;
    }

    public void ReassignEmployee(int newEmployeeId, string newToken, string deviceModel, string osVersion, string appVersion)
    {
        EmployeeId = newEmployeeId;
        FcmToken = newToken;
        DeviceModel = deviceModel;
        OsVersion = osVersion;
        AppVersion = appVersion;
        IsActive = true;
        LastActiveAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        LastActiveAt = DateTime.UtcNow;
    }
}

