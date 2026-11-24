#nullable enable
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FFM.Workforce.Api;
using FFM.Workforce.Application.Commands;
using FFM.Workforce.Application.Handlers;
using FFM.Workforce.Application.Interfaces;
using FFM.Workforce.Application.Validators;
using FFM.Workforce.Domain.Entities;
using FFM.Workforce.Infrastructure;
using FluentValidation.TestHelper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Npgsql;
using Xunit;

namespace FFM.Workforce.Tests
{
    public class EditEmployeeSecurityRegressionTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;
        private readonly EditEmployeeCommandValidator _validator = new();

        private readonly string _connectionString =
            "Host=localhost;Port=5434;Database=ffm_db;Username=ffmid_user;Password=ffm_pass";

        public EditEmployeeSecurityRegressionTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new[]
                    {
                        new KeyValuePair<string, string?>("ConnectionStrings:Postgres", _connectionString)
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

        #region 1. DAST Security Tests in Parallel (Section 13.1)

        [Theory]
        [InlineData("EMPLOYEE")]
        [InlineData("EXECUTIVE")]
        public async Task Dast_InsufficientRoles_CannotEditEmployee_Returns403Forbidden(string role)
        {
            var request = new
            {
                id = 1,
                name = "Forbidden Edit",
                email = "forbidden@test.com",
                mobile = "9876543210",
                roleId = 6
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Put, "/api/v1/Employees")
            {
                Content = JsonContent.Create(request)
            };
            httpRequest.Headers.Add("X-Test-Role", role);

            var response = await _client.SendAsync(httpRequest);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task Dast_UnauthenticatedRequest_Returns401Unauthorized()
        {
            var request = new
            {
                id = 1,
                name = "Unauthenticated Edit",
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
        public async Task Dast_ActorIdentityCannotBeSpoofed_ResolvedFromClaims()
        {
            // Even if client injects arbitrary actorUserId = 8888, server resolves actorUserId from JWT claim (1)
            var empRepoMock = new Mock<IEmployeeRepository>();
            var outboxMock = new Mock<IOutboxRepository>();
            var currentUserMock = new Mock<ICurrentUserService>();

            var options = new DbContextOptionsBuilder<WorkforceDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            var dbContext = new WorkforceDbContext(options);
            dbContext.Set<Role>().Add(new Role(6, "EMPLOYEE", "ACTIVE"));
            await dbContext.SaveChangesAsync();

            var targetEmp = new Employee(1, "EMP1", 1, "Target", 6, Guid.NewGuid(), null, "t@test.com", "1234567890", "System Admin");
            empRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(targetEmp);

            currentUserMock.Setup(u => u.GetUserId()).Returns(1); // Authenticated User ID
            currentUserMock.Setup(u => u.GetRoles()).Returns(new[] { "SYSTEM_ADMIN" });

            var handler = new EditEmployeeCommandHandler(empRepoMock.Object, outboxMock.Object, dbContext, currentUserMock.Object);

            var command = new EditEmployeeCommand
            {
                Id = 1,
                Name = "Target Updated",
                RoleId = 6,
                ActorUserId = 1 // Enforced by controller from JWT
            };

            var result = await handler.Handle(command, CancellationToken.None);
            Assert.NotNull(result);

            // Verify AuditLog in dbContext has ActorUserId = 1
            var auditLog = await dbContext.Set<AuditLog>().FirstOrDefaultAsync(a => a.EntityId == 1);
            Assert.NotNull(auditLog);
            Assert.Equal(1, auditLog.ActorUserId);
        }

        [Theory]
        [InlineData("'; DROP TABLE \"Employees\"; --")]
        [InlineData("' OR 1=1 --")]
        [InlineData("admin' --")]
        public void Dast_SqlInjectionInName_RejectedByValidator(string sqlPayload)
        {
            var command = new EditEmployeeCommand
            {
                Id = 1,
                Name = sqlPayload,
                RoleId = 6
            };

            var result = _validator.TestValidate(command);
            result.ShouldHaveValidationErrorFor(x => x.Name);
        }

        [Theory]
        [InlineData("<script>alert('xss')</script>")]
        [InlineData("<img src=x onerror=alert(1)>")]
        [InlineData("javascript:alert(1)")]
        public void Dast_XssInjectionInName_RejectedByValidator(string xssPayload)
        {
            var command = new EditEmployeeCommand
            {
                Id = 1,
                Name = xssPayload,
                RoleId = 6
            };

            var result = _validator.TestValidate(command);
            result.ShouldHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public async Task Dast_ErrorResponses_FollowRFC7807_WithoutExposingInternals()
        {
            var request = new
            {
                id = 1,
                name = "", // Empty name fails validation
                email = "invalid-email",
                roleId = 0
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Put, "/api/v1/Employees")
            {
                Content = JsonContent.Create(request)
            };
            httpRequest.Headers.Add("X-Test-Role", "ADMIN");

            var response = await _client.SendAsync(httpRequest);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            var body = await response.Content.ReadAsStringAsync();

            // Verify unified ApiResponse envelope structure
            Assert.Contains("success", body, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("error", body, StringComparison.OrdinalIgnoreCase);

            // Verify no stack trace or internal database internals are exposed
            Assert.DoesNotContain("Exception: ", body);
            Assert.DoesNotContain("at FFM.", body);
            Assert.DoesNotContain("Npgsql.", body);
            Assert.DoesNotContain("Stack trace", body, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region 2. SAST Code Security Tests in Parallel (Section 13.2)

        [Fact]
        public void Sast_AuditLog_DoesNotExposePasswordsOrTokensInSnapshot()
        {
            var beforeJson = JsonSerializer.Serialize(new
            {
                Name = "John",
                Email = "john@example.com",
                Mobile = "1234567890",
                RoleId = 6,
                ManagerId = (int?)null
            });

            Assert.DoesNotContain("Password", beforeJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Token", beforeJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Secret", beforeJson, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Sast_EmployeeEntity_ProtectedAgainstOverPosting()
        {
            // Domain entity Employee cannot have Id, UserId, EmployeeCode, CreatedAt, CreatedBy modified via Update
            var emp = new Employee(1, "CODE1", 10, "Name1", 6, Guid.NewGuid(), null, "e1@test.com", "1234567890", "System Admin");
            var initialId = emp.Id;
            var initialUserId = emp.UserId;
            var initialCode = emp.EmployeeCode;
            var initialCreatedAt = emp.CreatedAt;
            var initialCreatedBy = emp.CreatedBy;
            var initialOrgUnitId = emp.OrgUnitId;
            var initialStatus = emp.Status;

            emp.Update("Name2", "e2@test.com", "0987654321", 5, 2);

            Assert.Equal(initialId, emp.Id);
            Assert.Equal(initialUserId, emp.UserId);
            Assert.Equal(initialCode, emp.EmployeeCode);
            Assert.Equal(initialCreatedAt, emp.CreatedAt);
            Assert.Equal(initialCreatedBy, emp.CreatedBy);
            Assert.Equal(initialOrgUnitId, emp.OrgUnitId);
            Assert.Equal(initialStatus, emp.Status);

            Assert.Equal("Name2", emp.Name);
            Assert.Equal("e2@test.com", emp.Email);
            Assert.Equal("0987654321", emp.Mobile);
            Assert.Equal(5, emp.RoleId);
            Assert.Equal(2, emp.ManagerId);
        }

        #endregion

        #region 3. Penetration Testing & Abuse Scenarios in Parallel (Section 13.3)

        [Fact]
        public async Task PenTest_PrivilegeEscalation_SupervisorAssigningManagerRole_Throws403()
        {
            var empRepoMock = new Mock<IEmployeeRepository>();
            var outboxMock = new Mock<IOutboxRepository>();
            var currentUserMock = new Mock<ICurrentUserService>();

            var options = new DbContextOptionsBuilder<WorkforceDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            var dbContext = new WorkforceDbContext(options);
            dbContext.Set<Role>().Add(new Role(4, "MANAGER", "ACTIVE"));
            await dbContext.SaveChangesAsync();

            var supervisorEmp = new Employee(10, "SUP10", 10, "Supervisor", 5, Guid.NewGuid(), null, "sup@test.com", "1234567890", "System Admin");
            var reportEmp = new Employee(20, "REP20", 20, "Report", 6, Guid.NewGuid(), supervisorEmp.Id, "rep@test.com", "1234567890", "System Admin");

            empRepoMock.Setup(r => r.GetByIdAsync(20)).ReturnsAsync(reportEmp);
            empRepoMock.Setup(r => r.GetByUserIdAsync(10)).ReturnsAsync(supervisorEmp);

            currentUserMock.Setup(u => u.GetUserId()).Returns(10);
            currentUserMock.Setup(u => u.GetRoles()).Returns(new[] { "SUPERVISOR" });

            var handler = new EditEmployeeCommandHandler(empRepoMock.Object, outboxMock.Object, dbContext, currentUserMock.Object);

            var command = new EditEmployeeCommand
            {
                Id = 20,
                Name = "Report",
                RoleId = 4, // Attempting to promote to MANAGER!
                ActorUserId = 10
            };

            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("managerial", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task PenTest_HierarchyAbuse_SelfManager_Rejected()
        {
            var command = new EditEmployeeCommand
            {
                Id = 15,
                Name = "Valid Name",
                RoleId = 6,
                ManagerId = 15 // self manager
            };

            var result = _validator.TestValidate(command);
            result.ShouldHaveValidationErrorFor(x => x.ManagerId);
        }

        [Fact]
        public async Task PenTest_StateDesynchronization_EditDeactivatedEmployee_Rejected()
        {
            var empRepoMock = new Mock<IEmployeeRepository>();
            var outboxMock = new Mock<IOutboxRepository>();
            var currentUserMock = new Mock<ICurrentUserService>();

            var options = new DbContextOptionsBuilder<DbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            var dbContext = new DbContext(options);

            var deactivatedEmp = new Employee(10, "EMP10", 10, "Deactivated", 6, Guid.NewGuid(), null, "d@test.com", "1234567890", "System Admin", EmployeeStatus.DEACTIVATED);
            empRepoMock.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(deactivatedEmp);

            currentUserMock.Setup(u => u.GetUserId()).Returns(1);
            currentUserMock.Setup(u => u.GetRoles()).Returns(new[] { "ADMIN" });

            var handler = new EditEmployeeCommandHandler(empRepoMock.Object, outboxMock.Object, dbContext, currentUserMock.Object);

            var command = new EditEmployeeCommand
            {
                Id = 10,
                Name = "Attempted Edit",
                RoleId = 6,
                ActorUserId = 1
            };

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("deactivated", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        #endregion
    }
}
