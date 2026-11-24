using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FFM.FieldOperations.Application.Commands;
using FFM.FieldOperations.Application.DTOs;
using FFM.FieldOperations.Application.Handlers;
using FFM.FieldOperations.Application.Interfaces;
using FFM.FieldOperations.Application.Queries;
using FFM.FieldOperations.Domain.Entities;
using FFM.FieldOperations.Domain.Interfaces;
using FFM.FieldOperations.Domain.Models;
using Moq;
using Xunit;

namespace FFM.FieldOperations.Tests
{
    public class FieldOperationsSecurityTests
    {
        private readonly Mock<ITaskRepository> _taskRepoMock = new();
        private readonly Mock<IClientSiteRepository> _siteRepoMock = new();

        #region Security Check 1: Hardcoded Secrets & Sensitive Configs

        [Fact]
        public void Appsettings_DoesNotContainHardcodedPrivateKey()
        {
            var appsettingsPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "FFM.FieldOperations.Api", "appsettings.json");
            Assert.True(File.Exists(appsettingsPath), $"appsettings.json should exist at {appsettingsPath}");

            var json = File.ReadAllText(appsettingsPath);
            using var doc = JsonDocument.Parse(json);

            // Ensure no private key strings exist in config
            Assert.False(json.Contains("BEGIN RSA PRIVATE KEY", StringComparison.OrdinalIgnoreCase), "appsettings.json must NOT contain an RSA private key.");
            Assert.False(json.Contains("BEGIN PRIVATE KEY", StringComparison.OrdinalIgnoreCase), "appsettings.json must NOT contain a PKCS#8 private key.");
        }

        #endregion

        #region Security Check 2: Vertical Privilege Escalation & Authorization Guards

        [Fact]
        public async Task RegisterClientSite_EmployeeRole_ThrowsUnauthorizedAccessException()
        {
            var userMock = new Mock<ICurrentUserService>();
            userMock.Setup(u => u.GetRoles()).Returns(new List<string> { "EMPLOYEE" });
            userMock.Setup(u => u.GetUserId()).Returns(8);

            var handler = new CreateClientSiteCommandHandler(_siteRepoMock.Object, userMock.Object);
            var command = new CreateClientSiteCommand
            {
                ActorUserId = 8,
                SiteCode = "SITE-TEST-001",
                Name = "Unauthorized Site Registration Attempt",
                Latitude = 12.9715987m,
                Longitude = 77.5945627m,
                Address = "42 MG Road, Bengaluru, Karnataka"
            };

            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("Only Admins and Managers are authorized", ex.Message);
        }

        #endregion

        #region Security Check 3: BOLA / Horizontal Privilege Escalation Prevention

        [Fact]
        public async Task GetTasks_EmployeeCaller_EnforcesSelfScoping_PreventsBOLA()
        {
            // Attacker (Employee ID 8) attempts to query tasks belonging to Employee ID 97
            TaskFilterParameters? capturedParams = null;
            _taskRepoMock.Setup(r => r.GetPagedTasksAsync(It.IsAny<TaskFilterParameters>(), It.IsAny<CancellationToken>()))
                .Callback<TaskFilterParameters, CancellationToken>((p, _) => capturedParams = p)
                .ReturnsAsync((new List<TaskItem>(), 0));

            var handler = new GetTasksQueryHandler(_taskRepoMock.Object);
            var query = new GetTasksQuery
            {
                Filter = new GetTasksFilterQuery { EmployeeId = 97, Page = 1, PageSize = 20 },
                CallerUserId = 8,
                CallerRoles = new List<string> { "EMPLOYEE" }
            };

            await handler.Handle(query, CancellationToken.None);

            // Security Assertion: The query MUST be forcibly rewritten to the caller's own ID (8)
            Assert.NotNull(capturedParams);
            Assert.Equal(8, capturedParams.EmployeeId);
            Assert.NotEqual(97, capturedParams.EmployeeId);
        }

        #endregion

        #region Security Check 4: Business Logic & State Tampering Defenses

        [Fact]
        public async Task CreateTask_InactiveSite_BlocksWorkOrderInception()
        {
            var inactiveSite = new ClientSite(
                "SITE-INACT-001",
                "Decommissioned Branch",
                12.9715987m,
                77.5945627m,
                "42 MG Road, Bengaluru, Karnataka",
                501);

            inactiveSite.Deactivate();

            _siteRepoMock.Setup(s => s.GetByIdAsync(1))
                .ReturnsAsync(inactiveSite);

            var handler = new CreateTaskCommandHandler(_taskRepoMock.Object, _siteRepoMock.Object);
            var command = new CreateTaskCommand
            {
                ActorUserId = 12,
                Title = "Attempted Work On Inactive Site",
                ClientSiteId = 1,
                ScheduledStart = DateTime.UtcNow.AddHours(1),
                ScheduledEnd = DateTime.UtcNow.AddHours(2)
            };

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("is inactive and cannot accept new tasks", ex.Message);
        }

        [Fact]
        public async Task AssignTask_TerminalStateCompleted_BlocksReassignment()
        {
            var task = new TaskItem(
                "TSK-202610-0001",
                "Completed Substation Repair",
                "Finished transformer work",
                "URGENT",
                1,
                8,
                DateTime.UtcNow.AddHours(1),
                DateTime.UtcNow.AddHours(2),
                12);

            task.UpdateStatus("COMPLETED");

            _taskRepoMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(task);

            var handler = new AssignTaskCommandHandler(_taskRepoMock.Object, _siteRepoMock.Object);
            var command = new AssignTaskCommand
            {
                TaskId = 1,
                AssignedEmployeeId = 14,
                ActorUserId = 12
            };

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("Cannot reassign a task that is in 'COMPLETED' status", ex.Message);
        }

        #endregion

        #region Security Check 5: Input Validation & Boundary Stress

        [Fact]
        public async Task CreateTask_PastDateBeyond24Hours_BlocksTemporalSpoofing()
        {
            var handler = new CreateTaskCommandHandler(_taskRepoMock.Object, _siteRepoMock.Object);
            var command = new CreateTaskCommand
            {
                ActorUserId = 12,
                Title = "Fraudulent Historical Work Log",
                ClientSiteId = 1,
                ScheduledStart = DateTime.UtcNow.AddDays(-30), // 30 days in past
                ScheduledEnd = DateTime.UtcNow.AddDays(-30).AddHours(2)
            };

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("ScheduledStart cannot be set more than 24 hours in the past", ex.Message);
        }

        [Fact]
        public async Task GetTasks_LargePageSizeExceeding100_BlocksResourceExhaustion()
        {
            var handler = new GetTasksQueryHandler(_taskRepoMock.Object);
            var query = new GetTasksQuery
            {
                Filter = new GetTasksFilterQuery { PageSize = 10000 }, // Denial of service attempt
                CallerUserId = 12,
                CallerRoles = new List<string> { "ADMIN" }
            };

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(query, CancellationToken.None));
            Assert.Contains("PageSize must be between 1 and 100", ex.Message);
        }

        #endregion
    }
}
