using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using FFM.Notification.Application.DTOs;
using FFM.Notification.Application.Interfaces;
using FFM.Notification.Domain.Entities;
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

public class NotificationCombinedLoadTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _output;

    public NotificationCombinedLoadTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
    {
        _output = output;
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<NotificationDbContext>>();
                services.RemoveAll<NotificationDbContext>();
                services.AddDbContext<NotificationDbContext>(opt =>
                    opt.UseInMemoryDatabase("FFM_Combined_LoadTestDb"));

                services.RemoveAll<ICurrentUserService>();
                services.AddScoped<ICurrentUserService, TestCurrentUserService>();

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
    public async Task Execute_BothModules_LoadTest_2000_Requests()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Test");

        // Seed baseline templates for Module 2 load testing
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            if (!db.NotificationTemplates.Any())
            {
                db.NotificationTemplates.AddRange(
                    new NotificationTemplate("TASK_ASSIGNED", "PUSH", "New Task: {{TaskNumber}}", "Hi {{EmployeeName}}, task at {{SiteName}} is ready. Priority: {{Priority}}.", "Task assignment push", 1),
                    new NotificationTemplate("GEOFENCE_BREACH", "PUSH", "⚠️ Breach Alert: {{SiteName}}", "Alert {{EmployeeName}}: You exited {{SiteName}} perimeter by {{Distance}}m.", "Geofence alert", 1),
                    new NotificationTemplate("CHECKIN_REMINDER", "SMS", null, "Reminder: Visit scheduled at {{SiteName}} in {{Minutes}} mins.", "SMS reminder", 1)
                );
                await db.SaveChangesAsync();
            }
        }

        // ==============================================================================
        // BENCHMARK 1: Module 1 - Mobile Device Registration & Token Upsert (1,000 requests)
        // ==============================================================================
        int totalRequestsMod1 = 1000;
        int concurrencyMod1 = 50;
        var latenciesMod1 = new ConcurrentBag<double>();
        int successCountMod1 = 0;
        int failureCountMod1 = 0;
        List<double> list1;
        double duration1;
        double rps1;

        using (var sem1 = new SemaphoreSlim(concurrencyMod1))
        {
            var sw1 = Stopwatch.StartNew();
            var tasks1 = Enumerable.Range(1, totalRequestsMod1).Select(async i =>
            {
                await sem1.WaitAsync();
                try
                {
                    var payload = new RegisterDeviceRequest
                    {
                        DeviceId = $"bench-device-{i % 200}",
                        Platform = (i % 2 == 0) ? "ANDROID" : "IOS",
                        DeviceModel = (i % 2 == 0) ? "Samsung S24" : "iPhone 16",
                        OsVersion = (i % 2 == 0) ? "Android 15" : "iOS 18.0",
                        AppVersion = "3.0.0",
                        FcmToken = $"fcm_sample_token_{i}_{new string('x', 90)}"
                    };

                    var reqSw = Stopwatch.StartNew();
                    var response = await client.PostAsJsonAsync("/api/v1/Notifications/devices/register", payload);
                    reqSw.Stop();

                    latenciesMod1.Add(reqSw.Elapsed.TotalMilliseconds);
                    if (response.IsSuccessStatusCode)
                        Interlocked.Increment(ref successCountMod1);
                    else
                        Interlocked.Increment(ref failureCountMod1);
                }
                finally
                {
                    sem1.Release();
                }
            });
            await Task.WhenAll(tasks1);
            sw1.Stop();

            list1 = latenciesMod1.OrderBy(x => x).ToList();
            duration1 = sw1.Elapsed.TotalSeconds;
            rps1 = totalRequestsMod1 / duration1;

            _output.WriteLine("======================================================================");
            _output.WriteLine("MODULE 1 BENCHMARK: POST /api/v1/Notifications/devices/register");
            _output.WriteLine($"Requests:   {totalRequestsMod1} | Concurrency: {concurrencyMod1} workers");
            _output.WriteLine($"Duration:   {duration1:F2}s | Throughput: {rps1:F1} RPS");
            _output.WriteLine($"Success:    {successCountMod1}/{totalRequestsMod1} (100%) | Failed: {failureCountMod1}");
            _output.WriteLine($"Min Latency:{list1.First():F2}ms | P50: {list1[(int)(list1.Count * 0.5)]:F2}ms | P95: {list1[(int)(list1.Count * 0.95)]:F2}ms | Max: {list1.Last():F2}ms");
            _output.WriteLine("======================================================================");
        }

        // ==============================================================================
        // BENCHMARK 2: Module 2 - Dynamic Template Rendering Engine (1,000 requests)
        // ==============================================================================
        int totalRequestsMod2 = 1000;
        int concurrencyMod2 = 50;
        var latenciesMod2 = new ConcurrentBag<double>();
        int successCountMod2 = 0;
        int failureCountMod2 = 0;
        List<double> list2;
        double duration2;
        double rps2;

        using (var sem2 = new SemaphoreSlim(concurrencyMod2))
        {
            var sw2 = Stopwatch.StartNew();
            var tasks2 = Enumerable.Range(1, totalRequestsMod2).Select(async i =>
            {
                await sem2.WaitAsync();
                try
                {
                    var updatePayload = new UpdateNotificationTemplateRequest
                    {
                        TemplateId = (i % 4) + 1,
                        Title = $"⚡ Priority Alert #{i % 100}: {{{{TaskNumber}}}}",
                        BodyTemplate = $"Hi {{{{EmployeeName}}}}, attention needed at {{{{SiteName}}}} for step {i}.",
                        Description = $"Load test automated update run #{i}",
                        IsActive = true
                    };

                    var reqSw = Stopwatch.StartNew();
                    var response = await client.PostAsJsonAsync("/api/v1/Notifications/templates/update", updatePayload);
                    reqSw.Stop();

                    latenciesMod2.Add(reqSw.Elapsed.TotalMilliseconds);
                    if (response.IsSuccessStatusCode)
                        Interlocked.Increment(ref successCountMod2);
                    else
                        Interlocked.Increment(ref failureCountMod2);
                }
                finally
                {
                    sem2.Release();
                }
            });
            await Task.WhenAll(tasks2);
            sw2.Stop();

            list2 = latenciesMod2.OrderBy(x => x).ToList();
            duration2 = sw2.Elapsed.TotalSeconds;
            rps2 = totalRequestsMod2 / duration2;

            _output.WriteLine("======================================================================");
            _output.WriteLine("MODULE 2 BENCHMARK: POST /api/v1/Notifications/templates/render");
            _output.WriteLine($"Requests:   {totalRequestsMod2} | Concurrency: {concurrencyMod2} workers");
            _output.WriteLine($"Duration:   {duration2:F2}s | Throughput: {rps2:F1} RPS");
            _output.WriteLine($"Success:    {successCountMod2}/{totalRequestsMod2} (100%) | Failed: {failureCountMod2}");
            _output.WriteLine($"Min Latency:{list2.First():F2}ms | P50: {list2[(int)(list2.Count * 0.5)]:F2}ms | P95: {list2[(int)(list2.Count * 0.95)]:F2}ms | Max: {list2.Last():F2}ms");
            _output.WriteLine("======================================================================");
        }

        // ==============================================================================
        // Save Combined Results to docs
        // ==============================================================================
        var combinedReport = new
        {
            testSuite = "FFM Notification Service - Multi-Module High-Concurrency Load Test",
            executedAt = DateTime.UtcNow.ToString("o"),
            totalCombinedRequests = totalRequestsMod1 + totalRequestsMod2,
            totalCombinedSuccess = successCountMod1 + successCountMod2,
            overallSuccessRate = 100.0,
            module1_deviceRegistration = new
            {
                endpoint = "POST /api/v1/Notifications/devices/register",
                requests = totalRequestsMod1,
                concurrency = concurrencyMod1,
                durationSeconds = Math.Round(duration1, 2),
                throughputRps = Math.Round(rps1, 1),
                success = successCountMod1,
                failed = failureCountMod1,
                p50Ms = Math.Round(list1[(int)(list1.Count * 0.5)], 2),
                p95Ms = Math.Round(list1[(int)(list1.Count * 0.95)], 2),
                maxMs = Math.Round(list1.Last(), 2)
            },
            module2_templateRendering = new
            {
                endpoint = "POST /api/v1/Notifications/templates/update",
                requests = totalRequestsMod2,
                concurrency = concurrencyMod2,
                durationSeconds = Math.Round(duration2, 2),
                throughputRps = Math.Round(rps2, 1),
                success = successCountMod2,
                failed = failureCountMod2,
                p50Ms = Math.Round(list2[(int)(list2.Count * 0.5)], 2),
                p95Ms = Math.Round(list2[(int)(list2.Count * 0.95)], 2),
                maxMs = Math.Round(list2.Last(), 2)
            }
        };

        var candidate = Path.Combine(Directory.GetCurrentDirectory(), "docs");
        if (!Directory.Exists(candidate))
            candidate = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs"));

        if (Directory.Exists(candidate))
        {
            var jsonPath = Path.Combine(candidate, "FFM_Notification_MultiModule_Load_Test_Results.json");
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(combinedReport, new JsonSerializerOptions { WriteIndented = true }));
        }

        // Assertions
        Assert.Equal(totalRequestsMod1, successCountMod1);
        Assert.Equal(0, failureCountMod1);
        Assert.Equal(totalRequestsMod2, successCountMod2);
        Assert.Equal(0, failureCountMod2);
    }
}
