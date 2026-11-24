using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFM.Notification.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FFM.Notification.Tests;

public class FirebasePushDispatcherTests
{
    [Fact]
    public async Task SendAsync_WhenFcmTokenIsNullOrEmpty_ReturnsInvalidTokenFailure()
    {
        var configuration = new ConfigurationBuilder().Build();
        var dispatcher = new FirebasePushDispatcher(configuration, NullLogger<FirebasePushDispatcher>.Instance);

        var result = await dispatcher.SendAsync("", "Title", "Body", null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("INVALID_TOKEN", result.ErrorCode);
    }

    [Fact]
    public async Task SendAsync_WhenInvalidTokenFormatProvided_ReturnsHandledResult()
    {
        var configuration = new ConfigurationBuilder().Build();
        var dispatcher = new FirebasePushDispatcher(configuration, NullLogger<FirebasePushDispatcher>.Instance);

        var result = await dispatcher.SendAsync("invalid_token_format_123", "Title", "Body", null, CancellationToken.None);

        Assert.NotNull(result);
        if (!result.Success)
        {
            Assert.NotNull(result.ErrorCode);
        }
        else
        {
            Assert.NotNull(result.MessageId);
        }
    }
}
