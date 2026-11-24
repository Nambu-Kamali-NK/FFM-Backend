using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFM.FieldOperations.Application.DTOs;
using FFM.FieldOperations.Application.Handlers;
using FFM.FieldOperations.Application.Queries;
using FFM.FieldOperations.Domain.Entities;
using FFM.FieldOperations.Domain.Interfaces;
using FFM.FieldOperations.Domain.Models;
using Moq;
using Xunit;

namespace FFM.FieldOperations.Tests
{
    public class GetTasksQueryHandlerTests
    {
        private readonly Mock<ITaskRepository> _taskRepoMock = new();

        private ClientSite CreateActiveSite()
        {
            return new ClientSite(
                "SITE-BLR-001",
                "HDFC Bank Regional Office",
                12.9715987m,
                77.5945627m,
                "42 MG Road, Bengaluru, Karnataka",
                501,
                150,
                "Ramesh Kumar",
                "+919876543210",
                "ramesh@hdfcbank.com",
                "Bengaluru",
                "Karnataka",
                "560001");
        }

        private TaskItem CreateTask(string taskNumber, int? assignedEmployeeId)
        {
            return new TaskItem(
                taskNumber,
                "Quarterly POS Hardware Maintenance",
                "Inspect terminal, replace battery backup",
                "HIGH",
                1,
                assignedEmployeeId,
                DateTime.UtcNow.AddHours(2),
                DateTime.UtcNow.AddHours(4),
                12);
        }

        [Fact]
        public async Task GetTasks_AdminCaller_CanViewAllTasks()
        {
            // Arrange
            var task1 = CreateTask("TSK-202610-0001", 8);
            var task2 = CreateTask("TSK-202610-0002", 97);
            var items = new List<TaskItem> { task1, task2 };

            TaskFilterParameters? capturedParams = null;
            _taskRepoMock.Setup(r => r.GetPagedTasksAsync(It.IsAny<TaskFilterParameters>(), It.IsAny<CancellationToken>()))
                .Callback<TaskFilterParameters, CancellationToken>((p, _) => capturedParams = p)
                .ReturnsAsync((items, 2));

            var handler = new GetTasksQueryHandler(_taskRepoMock.Object);
            var query = new GetTasksQuery
            {
                Filter = new GetTasksFilterQuery { Page = 1, PageSize = 20 },
                CallerUserId = 12,
                CallerRoles = new List<string> { "ADMIN" }
            };

            // Act
            var result = await handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.TotalCount);
            Assert.Equal(2, result.Items.Count);
            Assert.NotNull(capturedParams);
            Assert.Null(capturedParams.EmployeeId); // No forced employee filter for admin
        }

        [Fact]
        public async Task GetTasks_AdminCaller_FiltersBySpecificEmployee()
        {
            // Arrange
            var task = CreateTask("TSK-202610-0001", 8);
            var items = new List<TaskItem> { task };

            TaskFilterParameters? capturedParams = null;
            _taskRepoMock.Setup(r => r.GetPagedTasksAsync(It.IsAny<TaskFilterParameters>(), It.IsAny<CancellationToken>()))
                .Callback<TaskFilterParameters, CancellationToken>((p, _) => capturedParams = p)
                .ReturnsAsync((items, 1));

            var handler = new GetTasksQueryHandler(_taskRepoMock.Object);
            var query = new GetTasksQuery
            {
                Filter = new GetTasksFilterQuery { EmployeeId = 8, Page = 1, PageSize = 20 },
                CallerUserId = 12,
                CallerRoles = new List<string> { "MANAGER" }
            };

            // Act
            var result = await handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result.Items);
            Assert.NotNull(capturedParams);
            Assert.Equal(8, capturedParams.EmployeeId);
        }

        [Fact]
        public async Task GetTasks_EmployeeCaller_ForcesSelfScoping_OverridesRequestedEmployeeId()
        {
            // Arrange
            var task = CreateTask("TSK-202610-0001", 8);
            var items = new List<TaskItem> { task };

            TaskFilterParameters? capturedParams = null;
            _taskRepoMock.Setup(r => r.GetPagedTasksAsync(It.IsAny<TaskFilterParameters>(), It.IsAny<CancellationToken>()))
                .Callback<TaskFilterParameters, CancellationToken>((p, _) => capturedParams = p)
                .ReturnsAsync((items, 1));

            var handler = new GetTasksQueryHandler(_taskRepoMock.Object);
            // Technician 8 attempts to view tasks for employee 97
            var query = new GetTasksQuery
            {
                Filter = new GetTasksFilterQuery { EmployeeId = 97, Page = 1, PageSize = 20 },
                CallerUserId = 8,
                CallerRoles = new List<string> { "EMPLOYEE" }
            };

            // Act
            var result = await handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(capturedParams);
            Assert.Equal(8, capturedParams.EmployeeId); // Overridden to caller's own ID
        }

        [Fact]
        public async Task GetTasks_InvertedDateRange_ThrowsArgumentException()
        {
            // Arrange
            var handler = new GetTasksQueryHandler(_taskRepoMock.Object);
            var query = new GetTasksQuery
            {
                Filter = new GetTasksFilterQuery
                {
                    StartDate = DateTime.UtcNow.AddDays(5),
                    EndDate = DateTime.UtcNow.AddDays(1) // End before start
                },
                CallerUserId = 12,
                CallerRoles = new List<string> { "ADMIN" }
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(query, CancellationToken.None));
            Assert.Contains("EndDate cannot be earlier than StartDate", ex.Message);
        }

        [Fact]
        public async Task GetTasks_InvalidStatus_ThrowsArgumentException()
        {
            // Arrange
            var handler = new GetTasksQueryHandler(_taskRepoMock.Object);
            var query = new GetTasksQuery
            {
                Filter = new GetTasksFilterQuery
                {
                    Status = "DRAFT" // Invalid status
                },
                CallerUserId = 12,
                CallerRoles = new List<string> { "ADMIN" }
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(query, CancellationToken.None));
            Assert.Contains("Invalid status 'DRAFT'", ex.Message);
        }

        [Fact]
        public async Task GetTasks_InvalidPriority_ThrowsArgumentException()
        {
            // Arrange
            var handler = new GetTasksQueryHandler(_taskRepoMock.Object);
            var query = new GetTasksQuery
            {
                Filter = new GetTasksFilterQuery
                {
                    Priority = "CRITICAL" // Invalid priority
                },
                CallerUserId = 12,
                CallerRoles = new List<string> { "ADMIN" }
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(query, CancellationToken.None));
            Assert.Contains("Invalid priority 'CRITICAL'", ex.Message);
        }

        [Fact]
        public async Task GetTasks_InvalidPageSize_ThrowsArgumentException()
        {
            // Arrange
            var handler = new GetTasksQueryHandler(_taskRepoMock.Object);
            var query = new GetTasksQuery
            {
                Filter = new GetTasksFilterQuery
                {
                    PageSize = 250 // Exceeds max 100
                },
                CallerUserId = 12,
                CallerRoles = new List<string> { "ADMIN" }
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(query, CancellationToken.None));
            Assert.Contains("PageSize must be between 1 and 100", ex.Message);
        }
    }
}
