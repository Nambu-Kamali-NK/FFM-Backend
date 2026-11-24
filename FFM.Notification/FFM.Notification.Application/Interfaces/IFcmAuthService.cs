namespace FFM.Notification.Application.Interfaces;

public class FcmTokenResult
{
    public string ProjectId { get; set; } = default!;
    public string TokenType { get; set; } = "Bearer";
    public string AccessToken { get; set; } = default!;
    public int ExpiresInSeconds { get; set; } = 3600;
}

public interface IFcmAuthService
{
    Task<FcmTokenResult> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}

