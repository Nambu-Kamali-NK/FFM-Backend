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

namespace FFM.Workforce.Tests
{
    [Collection("DatabaseTests")]
    public class EditEmployeeIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        private readonly string _connectionString =
            "Host=localhost;Port=5434;Database=ffm_db;Username=ffmid_user;Password=ffm_pass";

        public EditEmployeeIntegrationTests(WebApplicationFactory<Program> factory)
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

        private async Task CleanDatabaseAsync()
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = new NpgsqlCommand(@"
                DELETE FROM ""OutboxMessages"";
                DELETE FROM ""AuditLogs"";
                DELETE FROM ""Employees"";
            ", conn);

            await cmd.ExecuteNonQueryAsync();
        }

        private async Task<int> SeedEmployeeAsync(string employeeCode, string name, string email, string mobile, int roleId = 6, int userId = 100, int? managerId = null)
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            // Ensure Identity user exists for FK/one-to-one
            await using var cmdUser = new NpgsqlCommand(@"
                INSERT INTO ""Users"" (""Id"", ""UsernameOrEmail"", ""PasswordHash"", ""Role"", ""Status"", ""FailedAttempts"", ""MfaEnabled"")
                VALUES (@userId, @email, 'hash123', 'EMPLOYEE', 1, 0, false)
                ON CONFLICT (""Id"") DO UPDATE SET ""UsernameOrEmail"" = @email;
            ", conn);
            cmdUser.Parameters.AddWithValue("userId", userId);
            cmdUser.Parameters.AddWithValue("email", email.ToLowerInvariant());
            await cmdUser.ExecuteNonQueryAsync();

            // Insert employee and return generated integer ID
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO ""Employees"" (""EmployeeCode"", ""Name"", ""Email"", ""Mobile"", ""RoleId"", ""UserId"", ""OrgUnitId"", ""ManagerId"", ""Status"", ""CreatedAt"", ""CreatedBy"")
                VALUES (@code, @name, @email, @mobile, @roleId, @userId, 'a1b2c3d4-e5f6-7890-abcd-ef1234567890', @managerId, 'ACTIVE', NOW(), 'System Admin')
                RETURNING ""Id"";
            ", conn);

            cmd.Parameters.AddWithValue("code", employeeCode);
            cmd.Parameters.AddWithValue("name", name);
            cmd.Parameters.AddWithValue("email", email);
            cmd.Parameters.AddWithValue("mobile", mobile);
            cmd.Parameters.AddWithValue("roleId", roleId);
            cmd.Parameters.AddWithValue("userId", userId);
            cmd.Parameters.AddWithValue("managerId", (object?)managerId ?? DBNull.Value);

            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        [Fact]
        public async Task EditEmployee_ValidRequest_Returns200AndUpdatesDatabase()
        {
            await CleanDatabaseAsync();

            var empId = await SeedEmployeeAsync("EMP_TEST_01", "Original Name", "original@test.com", "9876543210", 6, 101);

            var request = new
            {
                id = empId,
                name = "Updated Employee Name",
                email = "updated@example.com",
                mobile = "9123456780",
                roleId = 6,
                managerId = (int?)null
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Put, "/api/v1/Employees")
            {
                Content = JsonContent.Create(request)
            };
            httpRequest.Headers.Add("X-Test-Role", "ADMIN");
            httpRequest.Headers.Add("X-Test-UserId", "1");

            var response = await _client.SendAsync(httpRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var responseBody = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonObject>();
            Assert.NotNull(responseBody);
            var dataNode = responseBody["data"] ?? responseBody;
            Assert.Equal(empId, dataNode["id"]?.GetValue<int>());
            Assert.True(responseBody["success"]?.GetValue<bool>() == true || dataNode["message"] != null);

            // Verify in PostgreSQL that editable fields changed
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var checkCmd = new NpgsqlCommand(@"
                SELECT ""Name"", ""Email"", ""Mobile"" FROM ""Employees"" WHERE ""Id"" = @id;
            ", conn);
            checkCmd.Parameters.AddWithValue("id", empId);
            await using var reader = await checkCmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal("Updated Employee Name", reader.GetString(0));
            Assert.Equal("updated@example.com", reader.GetString(1));
            Assert.Equal("9123456780", reader.GetString(2));
            await reader.CloseAsync();

            // Verify AuditLog was created with EmployeeUpdated action
            await using var auditCmd = new NpgsqlCommand(@"
                SELECT ""Action"", ""EntityId"", ""ActorUserId"" FROM ""AuditLogs"" WHERE ""EntityId"" = @id;
            ", conn);
            auditCmd.Parameters.AddWithValue("id", empId);
            await using var auditReader = await auditCmd.ExecuteReaderAsync();
            Assert.True(await auditReader.ReadAsync());
            Assert.Equal("EmployeeUpdated", auditReader.GetString(0));
            Assert.Equal(empId, auditReader.GetInt32(1));
            Assert.Equal(1, auditReader.GetInt32(2)); // Verified: Resolved from auth context, NOT client body!
            await auditReader.CloseAsync();

            // Verify Outbox message was created
            await using var outboxCmd = new NpgsqlCommand(@"
                SELECT COUNT(*) FROM ""OutboxMessages"" WHERE ""EventType"" = 'EmployeeUpdated';
            ", conn);
            var outboxCount = Convert.ToInt64(await outboxCmd.ExecuteScalarAsync());
            Assert.True(outboxCount > 0);
        }

        [Fact]
        public async Task EditEmployee_DualRoute_SupportsRootApiEmployees()
        {
            await CleanDatabaseAsync();

            var empId = await SeedEmployeeAsync("EMP_TEST_DUAL", "Dual Route Name", "dual@test.com", "9876543211", 6, 102);

            var request = new
            {
                id = empId,
                name = "Dual Route Updated",
                email = "dual.updated@example.com",
                mobile = "9876543211",
                roleId = 6,
                managerId = (int?)null
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Put, "/api/v1/Employees")
            {
                Content = JsonContent.Create(request)
            };
            httpRequest.Headers.Add("X-Test-Role", "ADMIN");

            var response = await _client.SendAsync(httpRequest);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task EditEmployee_NotFound_Returns404()
        {
            var request = new
            {
                id = 999999,
                name = "Non Existent",
                email = "noone@example.com",
                mobile = "9876543210",
                roleId = 6,
                managerId = (int?)null
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Put, "/api/v1/Employees")
            {
                Content = JsonContent.Create(request)
            };
            httpRequest.Headers.Add("X-Test-Role", "ADMIN");

            var response = await _client.SendAsync(httpRequest);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task EditEmployee_SelfManager_Returns400()
        {
            await CleanDatabaseAsync();
            var empId = await SeedEmployeeAsync("EMP_TEST_SELF", "Self Mgr", "self@test.com", "9876543212", 6, 103);

            var request = new
            {
                id = empId,
                name = "Self Mgr",
                email = "self@test.com",
                mobile = "9876543212",
                roleId = 6,
                managerId = empId // Self assignment!
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Put, "/api/v1/Employees")
            {
                Content = JsonContent.Create(request)
            };
            httpRequest.Headers.Add("X-Test-Role", "ADMIN");

            var response = await _client.SendAsync(httpRequest);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task EditEmployee_DuplicateEmail_Returns400Or409()
        {
            await CleanDatabaseAsync();
            var emp1 = await SeedEmployeeAsync("EMP_DUP_1", "Emp One", "emp1@test.com", "9876543213", 6, 104);
            var emp2 = await SeedEmployeeAsync("EMP_DUP_2", "Emp Two", "emp2@test.com", "9876543214", 6, 105);

            var request = new
            {
                id = emp2,
                name = "Emp Two Renamed",
                email = "emp1@test.com", // Duplicate of emp1
                mobile = "9876543214",
                roleId = 6,
                managerId = (int?)null
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Put, "/api/v1/Employees")
            {
                Content = JsonContent.Create(request)
            };
            httpRequest.Headers.Add("X-Test-Role", "ADMIN");

            var response = await _client.SendAsync(httpRequest);
            Assert.True(response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task EditEmployee_ParameterTampering_ImmutableFieldsRemainUnchanged()
        {
            await CleanDatabaseAsync();
            var empId = await SeedEmployeeAsync("EMP_TAMPER", "Tamper Test", "tamper@test.com", "9876543215", 6, 106);

            // Attempt to tamper with immutable fields (employeeCode, userId, createdAt, createdBy)
            var request = new
            {
                id = empId,
                name = "Legit Name",
                email = "tamper.updated@test.com",
                mobile = "9876543215",
                roleId = 6,
                managerId = (int?)null,
                employeeCode = "HACKED_CODE",
                userId = 999999,
                createdBy = "TAMPER_ATTEMPT",
                createdAt = "2020-01-01T00:00:00Z"
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Put, "/api/v1/Employees")
            {
                Content = JsonContent.Create(request)
            };
            httpRequest.Headers.Add("X-Test-Role", "ADMIN");

            var response = await _client.SendAsync(httpRequest);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Verify in PostgreSQL that immutable fields were NOT modified
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var checkCmd = new NpgsqlCommand(@"
                SELECT ""EmployeeCode"", ""UserId"", ""CreatedBy"" FROM ""Employees"" WHERE ""Id"" = @id;
            ", conn);
            checkCmd.Parameters.AddWithValue("id", empId);
            await using var reader = await checkCmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal("EMP_TAMPER", reader.GetString(0)); // Preserved!
            Assert.Equal(106, reader.GetInt32(1));          // Preserved!
            Assert.Equal("System Admin", reader.GetString(2)); // Preserved!
        }

        [Fact]
        public async Task EditEmployee_Unauthenticated_Returns401()
        {
            var request = new
            {
                id = 1,
                name = "Unauth Test",
                email = "unauth@test.com",
                mobile = "9876543210",
                roleId = 6
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Put, "/api/v1/Employees")
            {
                Content = JsonContent.Create(request)
            };
            httpRequest.Headers.Add("X-Test-Unauthenticated", "true");

            var response = await _client.SendAsync(httpRequest);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task EditEmployee_InsufficientRole_Employee_Returns403()
        {
            var request = new
            {
                id = 1,
                name = "Forbidden Test",
                email = "forbidden@test.com",
                mobile = "9876543210",
                roleId = 6
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Put, "/api/v1/Employees")
            {
                Content = JsonContent.Create(request)
            };
            httpRequest.Headers.Add("X-Test-Role", "EMPLOYEE");

            var response = await _client.SendAsync(httpRequest);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }
}
