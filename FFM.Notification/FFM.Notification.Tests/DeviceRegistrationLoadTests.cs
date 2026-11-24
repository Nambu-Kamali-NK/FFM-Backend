using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using FFM.Notification.Application.DTOs;
using FFM.Notification.Application.Interfaces;
using FFM.Notification.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace FFM.Notification.Tests;

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "12"),
            new Claim("userId", "12"),
            new Claim("sub", "12"),
            new Claim(ClaimTypes.Role, "EMPLOYEE"),
            new Claim(ClaimTypes.Role, "ADMIN")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public class TestCurrentUserService : ICurrentUserService
{
    public int GetUserId() => 12;
    public IEnumerable<string> GetRoles() => new[] { "EMPLOYEE" };
    public bool IsInRole(string role) => true;
}

public class DeviceRegistrationLoadTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _output;

    public DeviceRegistrationLoadTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
    {
        _output = output;
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                // Replace DbContext with InMemory database for benchmark
                services.RemoveAll<DbContextOptions<NotificationDbContext>>();
                services.RemoveAll<NotificationDbContext>();
                services.AddDbContext<NotificationDbContext>(opt =>
                    opt.UseInMemoryDatabase("FFM_Notification_LoadTestDb"));

                // Replace CurrentUserService
                services.RemoveAll<ICurrentUserService>();
                services.AddScoped<ICurrentUserService, TestCurrentUserService>();

                // Configure Test Authentication Scheme
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                    options.DefaultScheme = "Test";
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
            });
        });
    }

    [Fact]
    public async Task Execute_DeviceRegistration_LoadTest_1500_Requests()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Test");

        // 1. Warm-up phase (10 requests)
        for (int i = 0; i < 10; i++)
        {
            var warmupPayload = new RegisterDeviceRequest
            {
                DeviceId = $"warmup-device-{i}",
                Platform = "ANDROID",
                DeviceModel = "Samsung Galaxy S23",
                OsVersion = "Android 14",
                AppVersion = "1.0.0",
                FcmToken = new string('w', 120)
            };
            await client.PostAsJsonAsync("/api/v1/Notifications/devices/register", warmupPayload);
        }

        // 2. High-Concurrency Load Test
        int totalRequests = 1500;
        int maxConcurrency = 50;

        var latencies = new ConcurrentBag<double>();
        var statusCodes = new ConcurrentDictionary<HttpStatusCode, int>();
        int successCount = 0;
        int failureCount = 0;

        using var semaphore = new SemaphoreSlim(maxConcurrency);

        var stopwatch = Stopwatch.StartNew();

        var tasks = Enumerable.Range(1, totalRequests).Select(async i =>
        {
            await semaphore.WaitAsync();
            try
            {
                var payload = new RegisterDeviceRequest
                {
                    DeviceId = $"loadtest-device-{i % 250}", // Tests both new inserts and atomic upserts
                    Platform = (i % 2 == 0) ? "ANDROID" : "IOS",
                    DeviceModel = (i % 2 == 0) ? "Pixel 8 Pro" : "iPhone 15",
                    OsVersion = (i % 2 == 0) ? "Android 14" : "iOS 17.4",
                    AppVersion = "2.1.0",
                    FcmToken = $"fcm_token_sample_string_value_testing_long_token_string_{i}_{new string('k', 80)}"
                };

                var reqSw = Stopwatch.StartNew();
                var response = await client.PostAsJsonAsync("/api/v1/Notifications/devices/register", payload);
                reqSw.Stop();

                latencies.Add(reqSw.Elapsed.TotalMilliseconds);
                statusCodes.AddOrUpdate(response.StatusCode, 1, (_, count) => count + 1);

                if (response.IsSuccessStatusCode)
                {
                    Interlocked.Increment(ref successCount);
                }
                else
                {
                    Interlocked.Increment(ref failureCount);
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // 3. Statistical Analysis
        var latencyList = latencies.OrderBy(x => x).ToList();
        double totalSeconds = stopwatch.Elapsed.TotalSeconds;
        double rps = totalRequests / totalSeconds;
        double minLatency = latencyList.First();
        double maxLatency = latencyList.Last();
        double meanLatency = latencyList.Average();
        double p50 = latencyList[(int)(latencyList.Count * 0.50)];
        double p90 = latencyList[(int)(latencyList.Count * 0.90)];
        double p95 = latencyList[(int)(latencyList.Count * 0.95)];
        double p99 = latencyList[(int)(latencyList.Count * 0.99)];

        _output.WriteLine("======================================================================");
        _output.WriteLine("FFM NOTIFICATION SERVICE - LOAD TEST BENCHMARK REPORT");
        _output.WriteLine("======================================================================");
        _output.WriteLine($"Target Endpoint:    POST /api/v1/Notifications/devices/register");
        _output.WriteLine($"Status codes:       {string.Join(", ", statusCodes.Select(kv => $"{kv.Key}:{kv.Value}"))}");
        _output.WriteLine($"Total Requests:     {totalRequests}");
        _output.WriteLine($"Concurrency:        {maxConcurrency} parallel workers");
        _output.WriteLine($"Total Elapsed Time: {totalSeconds:F2} seconds");
        _output.WriteLine($"Throughput:         {rps:F1} Requests / Second (RPS)");
        _output.WriteLine($"Successful (200):   {successCount} ({((double)successCount / totalRequests) * 100:F1}%)");
        _output.WriteLine($"Failed:             {failureCount}");
        _output.WriteLine($"Min Latency:        {minLatency:F2} ms");
        _output.WriteLine($"Mean Latency:       {meanLatency:F2} ms");
        _output.WriteLine($"P50 (Median):       {p50:F2} ms");
        _output.WriteLine($"P90 Latency:        {p90:F2} ms");
        _output.WriteLine($"P95 Latency:        {p95:F2} ms");
        _output.WriteLine($"P99 Latency:        {p99:F2} ms");
        _output.WriteLine($"Max Latency:        {maxLatency:F2} ms");
        _output.WriteLine("======================================================================");

        // 4. Save JSON Results to docs
        var reportData = new
        {
            testName = "FFM Notification Service - Device Registration Load Test",
            endpoint = "POST /api/v1/Notifications/devices/register",
            executedAt = DateTime.UtcNow.ToString("o"),
            totalRequests = totalRequests,
            concurrency = maxConcurrency,
            totalDurationSeconds = Math.Round(totalSeconds, 2),
            throughputRps = Math.Round(rps, 1),
            successRatePercent = Math.Round(((double)successCount / totalRequests) * 100, 2),
            successfulRequests = successCount,
            failedRequests = failureCount,
            latenciesMs = new
            {
                min = Math.Round(minLatency, 2),
                mean = Math.Round(meanLatency, 2),
                p50 = Math.Round(p50, 2),
                p90 = Math.Round(p90, 2),
                p95 = Math.Round(p95, 2),
                p99 = Math.Round(p99, 2),
                max = Math.Round(maxLatency, 2)
            }
        };

        var candidate = Path.Combine(Directory.GetCurrentDirectory(), "docs");
        if (!Directory.Exists(candidate))
        {
            candidate = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs"));
        }
        if (Directory.Exists(candidate))
        {
            var jsonPath = Path.Combine(candidate, "FFM_Notification_Load_Test_Execution_Results.json");
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(reportData, new JsonSerializerOptions { WriteIndented = true }));
        }

        // Assertions for Load Test SLA
        Assert.Equal(totalRequests, successCount);
        Assert.Equal(0, failureCount);
        Assert.True(rps > 500, $"Expected RPS > 500, but got {rps}");
    }
}
