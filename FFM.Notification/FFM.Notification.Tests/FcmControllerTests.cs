using System;
using System.Threading;
using System.Threading.Tasks;
using FFM.Notification.Api.Controllers;
using FFM.Notification.Application.DTOs;
using FFM.Notification.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FFM.Notification.Tests;

public class FcmControllerTests
{
    private readonly Mock<IFcmAuthService> _fcmAuthServiceMock;
    private readonly FcmController _controller;

    public FcmControllerTests()
    {
        _fcmAuthServiceMock = new Mock<IFcmAuthService>();
        _controller = new FcmController(_fcmAuthServiceMock.Object);
    }

    [Fact]
    public async Task GetAccessToken_WhenSuccess_ReturnsOkWithGoogleToken()
    {
        var tokenResult = new FcmTokenResult
        {
            ProjectId = "ffm-notification-api",
            TokenType = "Bearer",
            AccessToken = "ya29.c.c0AZ4bNpFakeTokenForTesting...",
            ExpiresInSeconds = 3600
        };

        _fcmAuthServiceMock.Setup(s => s.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokenResult);

        var result = await _controller.GetAccessToken(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);

        var apiResp = Assert.IsType<ApiResponse<FcmTokenResult>>(okResult.Value);
        Assert.True(apiResp.Success);
        Assert.NotNull(apiResp.Data);
        Assert.Equal("ffm-notification-api", apiResp.Data.ProjectId);
        Assert.StartsWith("ya29.", apiResp.Data.AccessToken);
    }

    [Fact]
    public async Task GetAccessToken_WhenServiceThrows_Returns500InternalServerError()
    {
        _fcmAuthServiceMock.Setup(s => s.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Google OAuth2 server unreachable"));

        var result = await _controller.GetAccessToken(CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);

        var apiResp = Assert.IsType<ApiResponse<object>>(statusResult.Value);
        Assert.False(apiResp.Success);
        Assert.Equal("FCM_AUTH_FAILED", apiResp.Error?.Code);
    }
}

