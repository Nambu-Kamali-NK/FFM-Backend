#nullable enable
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FFM.Workforce.Api;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace FFM.Workforce.Tests;

[Collection("DatabaseTests")]
public class CreateEmployeeIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    private readonly string _connectionString =
        "Host=localhost;Port=5434;Database=ffm_db;Username=ffmid_user;Password=ffm_pass";

    private static readonly int ManagerRoleId = 4; // Assuming 4 is MANAGER

    private static readonly int ExistingUserId = 1;

    public CreateEmployeeIntegrationTests(
        WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new[]
                {
                    new System.Collections.Generic.KeyValuePair<string, string?>(
                        "ConnectionStrings:Postgres",
                        _connectionString)
                });
            });

            builder.ConfigureServices(services =>
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "TestAuthentication";
                    options.DefaultChallengeScheme = "TestAuthentication";
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    "TestAuthentication",
                    _ => { });
            });
        });

        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task CreateEmployee_AsManager_Succeeds()
    {
        await using var conn =
            new NpgsqlConnection(_connectionString);

        await conn.OpenAsync();

        await using var cmd =
            new NpgsqlCommand(
                "DELETE FROM \"Employees\"",
                conn);

        await cmd.ExecuteNonQueryAsync();

        await using var cmd2 =
            new NpgsqlCommand(
                "DELETE FROM \"AuditLogs\"",
                conn);

        await cmd2.ExecuteNonQueryAsync();

        await using var cmd3 =
            new NpgsqlCommand(
                "DELETE FROM \"OutboxMessages\"",
                conn);

        await cmd3.ExecuteNonQueryAsync();

        var employeeCode =
            "EMP" + Guid.NewGuid().ToString("N")[..8];

        var request = new
        {
            EmployeeCode = employeeCode,
            Name = "John Doe",
            Password = "SecurePassword123!",
            RoleId = ManagerRoleId,
            ManagerId = (int?)null,
            Email = $"{employeeCode}@example.com",
            Mobile = (string?)null
        };

        var response =
            await _client.PostAsJsonAsync(
                "/api/v1/employees",
                request);

        if (!response.IsSuccessStatusCode)
        {
            var body =
                await response.Content.ReadAsStringAsync();

            throw new Exception(
                $"Failed: {response.StatusCode}. Body: {body}");
        }

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        await using var cmdCheckAudit =
            new NpgsqlCommand(
                """
                SELECT COUNT(*)
                FROM "AuditLogs"
                WHERE "ActorUserId" = @actorUserId
                """,
                conn);

        cmdCheckAudit.Parameters.AddWithValue(
            "actorUserId",
            ExistingUserId);

        var auditResult =
            await cmdCheckAudit.ExecuteScalarAsync();

        var auditCount =
            Convert.ToInt64(auditResult);

        Assert.True(auditCount > 0);

        await using var cmdCheckOutbox =
            new NpgsqlCommand(
                """
                SELECT COUNT(*)
                FROM "OutboxMessages"
                WHERE "EventType" = 'EmployeeCreated'
                """,
                conn);

        var outboxResult =
            await cmdCheckOutbox.ExecuteScalarAsync();

        var outboxCount =
            Convert.ToInt64(outboxResult);

        Assert.True(outboxCount > 0);
    }
}
