using System.Threading;
using System.Threading.Tasks;
using FFM.Notification.Application.DTOs;
using FFM.Notification.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FFM.Notification.Api.Controllers;

[ApiController]
[Route("api/v1/Notifications/fcm")]
[Authorize]
public class FcmController : ControllerBase
{
    private readonly IFcmAuthService _fcmAuthService;

    public FcmController(IFcmAuthService fcmAuthService)
    {
        _fcmAuthService = fcmAuthService;
    }

    /// <summary>
    /// Authenticates with Google OAuth2 servers using the service account private key and generates a live Google Bearer Access Token for FCM HTTP v1 API.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Google OAuth2 Access Token details for project ffm-notification-api.</returns>
    [HttpGet("access-token")]
    [ProducesResponseType(typeof(ApiResponse<FcmTokenResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAccessToken(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _fcmAuthService.GetAccessTokenAsync(cancellationToken);
            return Ok(ApiResponse<FcmTokenResult>.Ok(result, "Successfully retrieved live Google OAuth2 Bearer Access Token for FCM."));
        }
        catch (System.Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<object>.Fail("FCM_AUTH_FAILED", ex.Message, "Failed to authenticate against Google OAuth2."));
        }
    }
}

