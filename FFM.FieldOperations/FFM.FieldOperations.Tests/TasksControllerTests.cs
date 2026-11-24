using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFM.FieldOperations.Api.Controllers;
using FFM.FieldOperations.Application.Commands;
using FFM.FieldOperations.Application.DTOs;
using FFM.FieldOperations.Application.Interfaces;
using FFM.FieldOperations.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FFM.FieldOperations.Tests
{
    public class TasksControllerTests
    {
        private readonly Mock<IMediator> _mediatorMock = new();
        private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
        private readonly TasksController _controller;

        public TasksControllerTests()
        {
            _currentUserServiceMock.Setup(s => s.GetUserId()).Returns(12);
            _currentUserServiceMock.Setup(s => s.GetRoles()).Returns(new List<string> { "ADMIN" });

            _controller = new TasksController(_mediatorMock.Object, _currentUserServiceMock.Object);
        }

        private TaskDto CreateSampleTaskDto(
            int id = 1,
            string taskNumber = "TSK-202610-0001",
            string status = "CREATED",
            int? assignedEmployeeId = null)
        {
            return new TaskDto
            {
                Id = id,
                TaskNumber = taskNumber,
                Title = "Quarterly Server Room Inspection",
                Description = "Inspect cooling unit and replace air filters",
                Priority = "HIGH",
                Status = status,
                ClientSiteId = 1,
                ClientSiteName = "HDFC Bank Regional Office",
                ClientSiteCode = "SITE-BLR-001",
                Address = "42 MG Road, Bengaluru",
                Latitude = 12.9715987m,
                Longitude = 77.5945627m,
                RadiusMeters = 150,
                AssignedEmployeeId = assignedEmployeeId,
                ScheduledStart = DateTime.UtcNow.AddHours(2),
                ScheduledEnd = DateTime.UtcNow.AddHours(4),
                CreatedByUserId = 12,
                CreatedAt = DateTime.UtcNow
            };
        }

        #region 1. CreateTask API Tests (POST /api/v1/Tasks)

        // --- Positive Cases ---

        [Fact]
        public async Task CreateTask_ValidPayload_Returns201CreatedWithTaskDto()
        {
            // Arrange
            var request = new CreateTaskRequest
            {
                Title = "Quarterly Server Room Inspection",
                Description = "Check cooling unit and power rails",
                Priority = "HIGH",
                ClientSiteId = 1,
                ScheduledStart = DateTime.UtcNow.AddHours(2),
                ScheduledEnd = DateTime.UtcNow.AddHours(5)
            };

            var expectedDto = CreateSampleTaskDto();
            _mediatorMock.Setup(m => m.Send(It.IsAny<CreateTaskCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedDto);

            // Act
            var result = await _controller.CreateTask(request, CancellationToken.None);

            // Assert
            var statusResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status201Created, statusResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<TaskDto>>(statusResult.Value);
            Assert.True(apiResponse.Success);
            Assert.NotNull(apiResponse.Data);
            Assert.Equal("TSK-202610-0001", apiResponse.Data.TaskNumber);
            Assert.Equal("CREATED", apiResponse.Data.Status);
        }

        [Fact]
        public async Task CreateTask_WithAssignedEmployee_Returns201CreatedWithAssignedStatus()
        {
            // Arrange
            var request = new CreateTaskRequest
            {
                Title = "Urgent ATM Sensor Fix",
                Priority = "URGENT",
                ClientSiteId = 1,
                AssignedEmployeeId = 8,
                ScheduledStart = DateTime.UtcNow.AddHours(1),
                ScheduledEnd = DateTime.UtcNow.AddHours(3)
            };

            var expectedDto = CreateSampleTaskDto(status: "ASSIGNED", assignedEmployeeId: 8);
            _mediatorMock.Setup(m => m.Send(It.IsAny<CreateTaskCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedDto);

            // Act
            var result = await _controller.CreateTask(request, CancellationToken.None);

            // Assert
            var statusResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status201Created, statusResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<TaskDto>>(statusResult.Value);
            Assert.Equal(8, apiResponse.Data!.AssignedEmployeeId);
            Assert.Equal("ASSIGNED", apiResponse.Data.Status);
        }

        [Theory]
        [InlineData("LOW")]
        [InlineData("MEDIUM")]
        [InlineData("HIGH")]
        [InlineData("URGENT")]
        public async Task CreateTask_AllValidPriorities_Succeeds(string priority)
        {
            // Arrange
            var request = new CreateTaskRequest
            {
                Title = $"Task with priority {priority}",
                Priority = priority,
                ClientSiteId = 1,
                ScheduledStart = DateTime.UtcNow.AddHours(2),
                ScheduledEnd = DateTime.UtcNow.AddHours(4)
            };

            var dto = CreateSampleTaskDto();
            dto.Priority = priority;
            _mediatorMock.Setup(m => m.Send(It.IsAny<CreateTaskCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(dto);

            // Act
            var result = await _controller.CreateTask(request, CancellationToken.None);

            // Assert
            var statusResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status201Created, statusResult.StatusCode);
            var apiResponse = Assert.IsType<ApiResponse<TaskDto>>(statusResult.Value);
            Assert.Equal(priority, apiResponse.Data!.Priority);
        }

        // --- Negative Cases ---

        [Fact]
        public async Task CreateTask_NullRequest_Returns400BadRequest()
        {
            // Act
            var result = await _controller.CreateTask(null!, CancellationToken.None);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
            Assert.False(apiResponse.Success);
            Assert.Equal("BAD_REQUEST", apiResponse.Error!.Code);
        }

        [Fact]
        public async Task CreateTask_NonExistentClientSite_Returns404NotFound()
        {
            // Arrange
            var request = new CreateTaskRequest
            {
                Title = "Maintenance for missing site",
                ClientSiteId = 9999,
                ScheduledStart = DateTime.UtcNow.AddHours(1),
                ScheduledEnd = DateTime.UtcNow.AddHours(3)
            };

            _mediatorMock.Setup(m => m.Send(It.IsAny<CreateTaskCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new KeyNotFoundException("Client site with ID 9999 was not found."));

            // Act
            var result = await _controller.CreateTask(request, CancellationToken.None);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(notFoundResult.Value);
            Assert.False(apiResponse.Success);
            Assert.Equal("CLIENT_SITE_NOT_FOUND", apiResponse.Error!.Code);
        }

        [Fact]
        public async Task CreateTask_InactiveClientSite_Returns400BadRequest()
        {
            // Arrange
            var request = new CreateTaskRequest
            {
                Title = "Maintenance for inactive site",
                ClientSiteId = 2,
                ScheduledStart = DateTime.UtcNow.AddHours(1),
                ScheduledEnd = DateTime.UtcNow.AddHours(3)
            };

            _mediatorMock.Setup(m => m.Send(It.IsAny<CreateTaskCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Cannot schedule tasks for inactive client site 'SITE-DEACT'."));

            // Act
            var result = await _controller.CreateTask(request, CancellationToken.None);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
            Assert.False(apiResponse.Success);
            Assert.Equal("CLIENT_SITE_INACTIVE", apiResponse.Error!.Code);
        }

        [Fact]
        public async Task CreateTask_ValidationArgumentError_Returns400BadRequest()
        {
            // Arrange
            var request = new CreateTaskRequest
            {
                Title = "Bad",
                ClientSiteId = 1,
                ScheduledStart = DateTime.UtcNow.AddHours(2),
                ScheduledEnd = DateTime.UtcNow.AddHours(1) // End before start
            };

            _mediatorMock.Setup(m => m.Send(It.IsAny<CreateTaskCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("ScheduledEnd must be strictly later than ScheduledStart."));

            // Act
            var result = await _controller.CreateTask(request, CancellationToken.None);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
            Assert.False(apiResponse.Success);
            Assert.Equal("VALIDATION_ERROR", apiResponse.Error!.Code);
        }

        [Fact]
        public async Task CreateTask_UnauthorizedCaller_Returns403Forbidden()
        {
            // Arrange
            var request = new CreateTaskRequest
            {
                Title = "Unauthorized creation attempt",
                ClientSiteId = 1,
                ScheduledStart = DateTime.UtcNow.AddHours(1),
                ScheduledEnd = DateTime.UtcNow.AddHours(3)
            };

            _mediatorMock.Setup(m => m.Send(It.IsAny<CreateTaskCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new UnauthorizedAccessException("Employees are not authorized to create work orders."));

            // Act
            var result = await _controller.CreateTask(request, CancellationToken.None);

            // Assert
            var statusResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status403Forbidden, statusResult.StatusCode);
            var apiResponse = Assert.IsType<ApiResponse<object>>(statusResult.Value);
            Assert.Equal("FORBIDDEN", apiResponse.Error!.Code);
        }

        // --- Edge Cases ---

        [Fact]
        public async Task CreateTask_TitleExactBoundary5Chars_Succeeds()
        {
            // Arrange
            var request = new CreateTaskRequest
            {
                Title = "12345", // Exactly 5 chars minimum
                ClientSiteId = 1,
                ScheduledStart = DateTime.UtcNow.AddHours(1),
                ScheduledEnd = DateTime.UtcNow.AddHours(2)
            };

            var dto = CreateSampleTaskDto();
            dto.Title = "12345";
            _mediatorMock.Setup(m => m.Send(It.IsAny<CreateTaskCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(dto);

            // Act
            var result = await _controller.CreateTask(request, CancellationToken.None);

            // Assert
            var statusResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status201Created, statusResult.StatusCode);
        }

        [Fact]
        public async Task CreateTask_TitleExactBoundary200Chars_Succeeds()
        {
            // Arrange
            var longTitle = new string('A', 200); // Exactly 200 chars maximum
            var request = new CreateTaskRequest
            {
                Title = longTitle,
                ClientSiteId = 1,
                ScheduledStart = DateTime.UtcNow.AddHours(1),
                ScheduledEnd = DateTime.UtcNow.AddHours(2)
            };

            var dto = CreateSampleTaskDto();
            dto.Title = longTitle;
            _mediatorMock.Setup(m => m.Send(It.IsAny<CreateTaskCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(dto);

            // Act
            var result = await _controller.CreateTask(request, CancellationToken.None);

            // Assert
            var statusResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status201Created, statusResult.StatusCode);
        }

        [Fact]
        public async Task CreateTask_SpecialCharactersAndUnicodeInTitle_HandledSafely()
        {
            // Arrange
            var specialTitle = "Maintenance @ Bank Branch #4 & POS (₹ / $ / €)";
            var request = new CreateTaskRequest
            {
                Title = specialTitle,
                ClientSiteId = 1,
                ScheduledStart = DateTime.UtcNow.AddHours(1),
                ScheduledEnd = DateTime.UtcNow.AddHours(3)
            };

            var dto = CreateSampleTaskDto();
            dto.Title = specialTitle;
            _mediatorMock.Setup(m => m.Send(It.IsAny<CreateTaskCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(dto);

            // Act
            var result = await _controller.CreateTask(request, CancellationToken.None);

            // Assert
            var statusResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status201Created, statusResult.StatusCode);
        }

        [Fact]
        public async Task CreateTask_NullDescription_Succeeds()
        {
            // Arrange
            var request = new CreateTaskRequest
            {
                Title = "Task without description",
                Description = null,
                ClientSiteId = 1,
                ScheduledStart = DateTime.UtcNow.AddHours(1),
                ScheduledEnd = DateTime.UtcNow.AddHours(2)
            };

            var dto = CreateSampleTaskDto();
            dto.Description = null;
            _mediatorMock.Setup(m => m.Send(It.IsAny<CreateTaskCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(dto);

            // Act
            var result = await _controller.CreateTask(request, CancellationToken.None);

            // Assert
            var statusResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status201Created, statusResult.StatusCode);
            var apiResponse = Assert.IsType<ApiResponse<TaskDto>>(statusResult.Value);
            Assert.Null(apiResponse.Data!.Description);
        }

        #endregion

        #region 2. ReassignTask API Tests (POST /api/v1/tasks/reassign)

        // --- Positive Cases ---

        [Fact]
        public async Task AssignTask_ValidPayload_Returns200OKWithUpdatedTask()
        {
            // Arrange
            var request = new AssignTaskRequest
            {
                TaskId = 1,
                AssignedEmployeeId = 8
            };

            var expectedDto = CreateSampleTaskDto(id: 1, status: "ASSIGNED", assignedEmployeeId: 8);
            _mediatorMock.Setup(m => m.Send(It.IsAny<AssignTaskCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedDto);

            // Act
            var result = await _controller.ReassignTask(request, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<TaskDto>>(okResult.Value);
            Assert.True(apiResponse.Success);
            Assert.Equal("ASSIGNED", apiResponse.Data!.Status);
            Assert.Equal(8, apiResponse.Data.AssignedEmployeeId);
        }

        [Fact]
        public async Task AssignTask_ReassignsToDifferentTechnician_Returns200OK()
        {
            // Arrange
            var request = new AssignTaskRequest
            {
                TaskId = 1,
                AssignedEmployeeId = 22 // New technician
            };

            var expectedDto = CreateSampleTaskDto(id: 1, status: "ASSIGNED", assignedEmployeeId: 22);
            _mediatorMock.Setup(m => m.Send(It.IsAny<AssignTaskCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedDto);

            // Act
            var result = await _controller.ReassignTask(request, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<TaskDto>>(okResult.Value);
            Assert.Equal(22, apiResponse.Data!.AssignedEmployeeId);
        }

        // --- Negative Cases ---

        [Fact]
        public async Task AssignTask_NullRequest_Returns400BadRequest()
        {
            // Act
            var result = await _controller.ReassignTask(null!, CancellationToken.None);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
            Assert.False(apiResponse.Success);
            Assert.Equal("BAD_REQUEST", apiResponse.Error!.Code);
        }

        [Fact]
        public async Task AssignTask_NonExistentTaskId_Returns404NotFound()
        {
            // Arrange
            var request = new AssignTaskRequest { TaskId = 9999, AssignedEmployeeId = 8 };
            _mediatorMock.Setup(m => m.Send(It.IsAny<AssignTaskCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new KeyNotFoundException("Task with ID 9999 was not found."));

            // Act
            var result = await _controller.ReassignTask(request, CancellationToken.None);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(notFoundResult.Value);
            Assert.Equal("TASK_NOT_FOUND", apiResponse.Error!.Code);
        }

        [Theory]
        [InlineData("COMPLETED")]
        [InlineData("CANCELLED")]
        [InlineData("CHECKED_IN")]
        public async Task AssignTask_TerminalOrInProgressState_Returns400BadRequest(string terminalStatus)
        {
            // Arrange
            var request = new AssignTaskRequest { TaskId = 1, AssignedEmployeeId = 8 };
            _mediatorMock.Setup(m => m.Send(It.IsAny<AssignTaskCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException($"Cannot reassign a task that is in '{terminalStatus}' status."));

            // Act
            var result = await _controller.ReassignTask(request, CancellationToken.None);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
            Assert.Equal("INVALID_TASK_STATE", apiResponse.Error!.Code);
        }

        [Fact]
        public async Task AssignTask_NegativeEmployeeId_Returns400BadRequest()
        {
            // Arrange
            var request = new AssignTaskRequest { TaskId = 1, AssignedEmployeeId = -5 };
            _mediatorMock.Setup(m => m.Send(It.IsAny<AssignTaskCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Invalid Employee ID."));

            // Act
            var result = await _controller.ReassignTask(request, CancellationToken.None);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
            Assert.Equal("VALIDATION_ERROR", apiResponse.Error!.Code);
        }

        [Fact]
        public async Task AssignTask_UnauthorizedCaller_Returns403Forbidden()
        {
            // Arrange
            var request = new AssignTaskRequest { TaskId = 1, AssignedEmployeeId = 8 };
            _mediatorMock.Setup(m => m.Send(It.IsAny<AssignTaskCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new UnauthorizedAccessException("Employees cannot assign work orders."));

            // Act
            var result = await _controller.ReassignTask(request, CancellationToken.None);

            // Assert
            var statusResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status403Forbidden, statusResult.StatusCode);
            var apiResponse = Assert.IsType<ApiResponse<object>>(statusResult.Value);
            Assert.Equal("FORBIDDEN", apiResponse.Error!.Code);
        }

        // --- Edge Cases ---

        [Fact]
        public async Task AssignTask_ReassignToSameTechnician_IdempotentSuccess()
        {
            // Arrange
            var request = new AssignTaskRequest { TaskId = 1, AssignedEmployeeId = 8 };
            var expectedDto = CreateSampleTaskDto(status: "ASSIGNED", assignedEmployeeId: 8);

            _mediatorMock.Setup(m => m.Send(It.IsAny<AssignTaskCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedDto);

            // Act
            var result = await _controller.ReassignTask(request, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<TaskDto>>(okResult.Value);
            Assert.Equal(8, apiResponse.Data!.AssignedEmployeeId);
        }

        [Fact]
        public async Task AssignTask_MaxIntTaskId_HandlesCleanly()
        {
            // Arrange
            var request = new AssignTaskRequest { TaskId = int.MaxValue, AssignedEmployeeId = 8 };
            _mediatorMock.Setup(m => m.Send(It.IsAny<AssignTaskCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new KeyNotFoundException($"Task with ID {int.MaxValue} was not found."));

            // Act
            var result = await _controller.ReassignTask(request, CancellationToken.None);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal(StatusCodes.Status404NotFound, notFoundResult.StatusCode);
        }

        #endregion

        #region 3. GetTasks API Tests (GET /api/v1/Tasks)

        // --- Positive Cases ---

        [Fact]
        public async Task GetTasks_AdminCaller_Returns200OKWithPagedResult()
        {
            // Arrange
            var filter = new GetTasksFilterQuery { Page = 1, PageSize = 20 };
            var items = new List<TaskDto> { CreateSampleTaskDto(1), CreateSampleTaskDto(2) };
            var pagedResult = new PagedResult<TaskDto>
            {
                Items = items,
                Page = 1,
                PageSize = 20,
                TotalCount = 2
            };

            _mediatorMock.Setup(m => m.Send(It.IsAny<GetTasksQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(pagedResult);

            // Act
            var result = await _controller.GetTasks(filter, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<PagedResult<TaskDto>>>(okResult.Value);
            Assert.True(apiResponse.Success);
            Assert.Equal(2, apiResponse.Data!.TotalCount);
            Assert.Equal(2, apiResponse.Data.Items.Count);
        }

        [Fact]
        public async Task GetTasks_WithAllFilterParameters_PassesFiltersCorrectly()
        {
            // Arrange
            var filter = new GetTasksFilterQuery
            {
                EmployeeId = 8,
                Status = "ASSIGNED",
                ClientSiteId = 1,
                Priority = "HIGH",
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow.AddDays(1),
                Search = "server",
                Page = 1,
                PageSize = 10,
                SortBy = "priority",
                SortOrder = "desc"
            };

            GetTasksQuery? capturedQuery = null;
            _mediatorMock.Setup(m => m.Send(It.IsAny<GetTasksQuery>(), It.IsAny<CancellationToken>()))
                .Callback<IRequest<PagedResult<TaskDto>>, CancellationToken>((q, _) => capturedQuery = q as GetTasksQuery)
                .ReturnsAsync(new PagedResult<TaskDto> { Items = new List<TaskDto>(), TotalCount = 0 });

            // Act
            var result = await _controller.GetTasks(filter, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(capturedQuery);
            Assert.Equal(8, capturedQuery!.Filter.EmployeeId);
            Assert.Equal("ASSIGNED", capturedQuery.Filter.Status);
            Assert.Equal("server", capturedQuery.Filter.Search);
            Assert.Equal(12, capturedQuery.CallerUserId);
        }

        // --- Negative Cases ---

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        public async Task GetTasks_PageLessThanOne_Returns400BadRequest(int page)
        {
            // Arrange
            var filter = new GetTasksFilterQuery { Page = page, PageSize = 20 };
            _mediatorMock.Setup(m => m.Send(It.IsAny<GetTasksQuery>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Page must be at least 1."));

            // Act
            var result = await _controller.GetTasks(filter, CancellationToken.None);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
            Assert.Equal("VALIDATION_ERROR", apiResponse.Error!.Code);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        [InlineData(101)]
        [InlineData(999999)]
        public async Task GetTasks_PageSizeOutOfBounds_Returns400BadRequest(int pageSize)
        {
            // Arrange
            var filter = new GetTasksFilterQuery { Page = 1, PageSize = pageSize };
            _mediatorMock.Setup(m => m.Send(It.IsAny<GetTasksQuery>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("PageSize must be between 1 and 100."));

            // Act
            var result = await _controller.GetTasks(filter, CancellationToken.None);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
            Assert.Equal("VALIDATION_ERROR", apiResponse.Error!.Code);
        }

        [Fact]
        public async Task GetTasks_InvalidStatusQuery_Returns400BadRequest()
        {
            // Arrange
            var filter = new GetTasksFilterQuery { Status = "INVALID_STATUS_NAME" };
            _mediatorMock.Setup(m => m.Send(It.IsAny<GetTasksQuery>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Invalid status 'INVALID_STATUS_NAME'."));

            // Act
            var result = await _controller.GetTasks(filter, CancellationToken.None);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
            Assert.Equal("VALIDATION_ERROR", apiResponse.Error!.Code);
        }

        [Fact]
        public async Task GetTasks_InvalidPriorityQuery_Returns400BadRequest()
        {
            // Arrange
            var filter = new GetTasksFilterQuery { Priority = "SUPER_CRITICAL" };
            _mediatorMock.Setup(m => m.Send(It.IsAny<GetTasksQuery>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Invalid priority 'SUPER_CRITICAL'."));

            // Act
            var result = await _controller.GetTasks(filter, CancellationToken.None);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
            Assert.Equal("VALIDATION_ERROR", apiResponse.Error!.Code);
        }

        [Fact]
        public async Task GetTasks_EndDateBeforeStartDate_Returns400BadRequest()
        {
            // Arrange
            var filter = new GetTasksFilterQuery
            {
                StartDate = DateTime.UtcNow.AddDays(5),
                EndDate = DateTime.UtcNow.AddDays(1) // End before start
            };

            _mediatorMock.Setup(m => m.Send(It.IsAny<GetTasksQuery>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("EndDate cannot be earlier than StartDate."));

            // Act
            var result = await _controller.GetTasks(filter, CancellationToken.None);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
            Assert.Equal("VALIDATION_ERROR", apiResponse.Error!.Code);
        }

        // --- Edge Cases ---

        [Fact]
        public async Task GetTasks_ZeroRecordsMatched_Returns200OKWithEmptyList()
        {
            // Arrange
            var filter = new GetTasksFilterQuery { Page = 1, PageSize = 20, Search = "nonexistent_work_order_xyz" };
            var pagedResult = new PagedResult<TaskDto>
            {
                Items = new List<TaskDto>(),
                Page = 1,
                PageSize = 20,
                TotalCount = 0
            };

            _mediatorMock.Setup(m => m.Send(It.IsAny<GetTasksQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(pagedResult);

            // Act
            var result = await _controller.GetTasks(filter, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<PagedResult<TaskDto>>>(okResult.Value);
            Assert.NotNull(apiResponse.Data);
            Assert.Empty(apiResponse.Data.Items);
            Assert.Equal(0, apiResponse.Data.TotalCount);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(100)]
        public async Task GetTasks_PageSizeExactBoundaries_1And100_Succeeds(int boundaryPageSize)
        {
            // Arrange
            var filter = new GetTasksFilterQuery { Page = 1, PageSize = boundaryPageSize };
            var pagedResult = new PagedResult<TaskDto>
            {
                Items = new List<TaskDto>(),
                Page = 1,
                PageSize = boundaryPageSize,
                TotalCount = 0
            };

            _mediatorMock.Setup(m => m.Send(It.IsAny<GetTasksQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(pagedResult);

            // Act
            var result = await _controller.GetTasks(filter, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<PagedResult<TaskDto>>>(okResult.Value);
            Assert.Equal(boundaryPageSize, apiResponse.Data!.PageSize);
        }

        [Fact]
        public async Task GetTasks_SearchWithSqlWildcardCharacters_HandledSafely()
        {
            // Arrange
            var filter = new GetTasksFilterQuery { Search = "%_'; DROP TABLE Tasks;--" };
            var pagedResult = new PagedResult<TaskDto>
            {
                Items = new List<TaskDto>(),
                Page = 1,
                PageSize = 20,
                TotalCount = 0
            };

            _mediatorMock.Setup(m => m.Send(It.IsAny<GetTasksQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(pagedResult);

            // Act
            var result = await _controller.GetTasks(filter, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
        }

        #endregion

        #region 4. CancelTask API Tests (POST /api/v1/tasks/cancel)

        [Fact]
        public async Task CancelTask_ValidPayload_Returns200OkWithCancelledTaskDto()
        {
            // Arrange
            var request = new CancelTaskRequest
            {
                TaskId = 1,
                Reason = "Customer postponed maintenance to next month."
            };

            var cancelledDto = CreateSampleTaskDto(id: 1, status: "CANCELLED");
            cancelledDto.CancellationReason = request.Reason;
            cancelledDto.CancelledAt = DateTime.UtcNow;

            _mediatorMock.Setup(m => m.Send(It.IsAny<CancelTaskCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(cancelledDto);

            // Act
            var result = await _controller.CancelTask(request, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<TaskDto>>(okResult.Value);
            Assert.True(apiResponse.Success);
            Assert.NotNull(apiResponse.Data);
            Assert.Equal("CANCELLED", apiResponse.Data.Status);
            Assert.Equal(request.Reason, apiResponse.Data.CancellationReason);
        }

        [Fact]
        public async Task CancelTask_NullRequest_Returns400BadRequest()
        {
            // Act
            var result = await _controller.CancelTask(null!, CancellationToken.None);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
            Assert.False(apiResponse.Success);
            Assert.Equal("BAD_REQUEST", apiResponse.Error!.Code);
        }

        [Fact]
        public async Task CancelTask_NonExistentTask_Returns404NotFound()
        {
            // Arrange
            var request = new CancelTaskRequest
            {
                TaskId = 99999,
                Reason = "Valid cancellation reason string."
            };

            _mediatorMock.Setup(m => m.Send(It.IsAny<CancelTaskCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new KeyNotFoundException("Task with ID 99999 was not found."));

            // Act
            var result = await _controller.CancelTask(request, CancellationToken.None);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(notFoundResult.Value);
            Assert.False(apiResponse.Success);
            Assert.Equal("TASK_NOT_FOUND", apiResponse.Error!.Code);
        }

        [Fact]
        public async Task CancelTask_CheckedInState_Returns400BadRequest()
        {
            // Arrange
            var request = new CancelTaskRequest
            {
                TaskId = 5,
                Reason = "Emergency cancellation requested."
            };

            _mediatorMock.Setup(m => m.Send(It.IsAny<CancelTaskCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Cannot cancel a task that is in 'CHECKED_IN' status."));

            // Act
            var result = await _controller.CancelTask(request, CancellationToken.None);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
            Assert.False(apiResponse.Success);
            Assert.Equal("INVALID_TASK_STATE", apiResponse.Error!.Code);
        }

        [Fact]
        public async Task CancelTask_ValidationError_Returns400BadRequest()
        {
            // Arrange
            var request = new CancelTaskRequest
            {
                TaskId = 1,
                Reason = "Bad" // Too short
            };

            _mediatorMock.Setup(m => m.Send(It.IsAny<CancelTaskCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Cancellation reason must be at least 5 characters."));

            // Act
            var result = await _controller.CancelTask(request, CancellationToken.None);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
            Assert.False(apiResponse.Success);
            Assert.Equal("VALIDATION_ERROR", apiResponse.Error!.Code);
        }

        [Fact]
        public async Task CancelTask_UnauthorizedCaller_Returns403Forbidden()
        {
            // Arrange
            var request = new CancelTaskRequest
            {
                TaskId = 1,
                Reason = "Field tech cancellation attempt"
            };

            _mediatorMock.Setup(m => m.Send(It.IsAny<CancelTaskCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new UnauthorizedAccessException("Employees are not authorized to cancel work orders."));

            // Act
            var result = await _controller.CancelTask(request, CancellationToken.None);

            // Assert
            var statusResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status403Forbidden, statusResult.StatusCode);
            var apiResponse = Assert.IsType<ApiResponse<object>>(statusResult.Value);
            Assert.Equal("FORBIDDEN", apiResponse.Error!.Code);
        }

        #endregion

        #region UpdateTaskStatus Tests

        [Fact]
        public async Task UpdateTaskStatus_ValidRequest_Returns200Ok()
        {
            // Arrange
            var request = new UpdateTaskStatusRequest
            {
                TaskId = 105,
                Status = "EN_ROUTE",
                Notes = "Departing for site"
            };

            var expectedDto = new TaskDto
            {
                Id = 105,
                TaskNumber = "TSK-202610-0105",
                Title = "Emergency ATM Repair",
                Status = "EN_ROUTE",
                Priority = "URGENT",
                ClientSiteId = 1,
                AssignedEmployeeId = 12
            };

            _mediatorMock.Setup(m => m.Send(It.IsAny<UpdateTaskStatusCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedDto);

            // Act
            var result = await _controller.UpdateTaskStatus(request, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<TaskDto>>(okResult.Value);
            Assert.True(apiResponse.Success);
            Assert.Equal("EN_ROUTE", apiResponse.Data!.Status);
            Assert.Equal("Task status updated to EN_ROUTE successfully.", apiResponse.Message);
        }

        [Fact]
        public async Task UpdateTaskStatus_NullRequest_Returns400BadRequest()
        {
            // Act
            var result = await _controller.UpdateTaskStatus(null!, CancellationToken.None);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
            Assert.False(apiResponse.Success);
            Assert.Equal("BAD_REQUEST", apiResponse.Error!.Code);
        }

        [Fact]
        public async Task UpdateTaskStatus_TaskNotFound_Returns404NotFound()
        {
            // Arrange
            var request = new UpdateTaskStatusRequest
            {
                TaskId = 999999,
                Status = "EN_ROUTE"
            };

            _mediatorMock.Setup(m => m.Send(It.IsAny<UpdateTaskStatusCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new KeyNotFoundException("Task with ID 999999 was not found."));

            // Act
            var result = await _controller.UpdateTaskStatus(request, CancellationToken.None);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(notFoundResult.Value);
            Assert.False(apiResponse.Success);
            Assert.Equal("TASK_NOT_FOUND", apiResponse.Error!.Code);
        }

        [Fact]
        public async Task UpdateTaskStatus_BOLAForbidden_Returns403Forbidden()
        {
            // Arrange
            var request = new UpdateTaskStatusRequest
            {
                TaskId = 105,
                Status = "EN_ROUTE"
            };

            _mediatorMock.Setup(m => m.Send(It.IsAny<UpdateTaskStatusCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new UnauthorizedAccessException("Access denied: You are not authorized to update tasks assigned to another employee."));

            // Act
            var result = await _controller.UpdateTaskStatus(request, CancellationToken.None);

            // Assert
            var statusResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status403Forbidden, statusResult.StatusCode);
            var apiResponse = Assert.IsType<ApiResponse<object>>(statusResult.Value);
            Assert.Equal("TASK_ACCESS_DENIED", apiResponse.Error!.Code);
        }

        [Fact]
        public async Task UpdateTaskStatus_InvalidStatusTransition_Returns400BadRequest()
        {
            // Arrange
            var request = new UpdateTaskStatusRequest
            {
                TaskId = 105,
                Status = "EN_ROUTE"
            };

            _mediatorMock.Setup(m => m.Send(It.IsAny<UpdateTaskStatusCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Cannot transition task to EN_ROUTE from current status 'CHECKED_IN'."));

            // Act
            var result = await _controller.UpdateTaskStatus(request, CancellationToken.None);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
            Assert.False(apiResponse.Success);
            Assert.Equal("INVALID_TASK_STATE", apiResponse.Error!.Code);
        }

        [Fact]
        public async Task UpdateTaskStatus_ValidationError_Returns400BadRequest()
        {
            // Arrange
            var request = new UpdateTaskStatusRequest
            {
                TaskId = 105,
                Status = "INVALID_STATUS"
            };

            _mediatorMock.Setup(m => m.Send(It.IsAny<UpdateTaskStatusCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Status 'INVALID_STATUS' is invalid. Permitted status for this endpoint is 'EN_ROUTE'."));

            // Act
            var result = await _controller.UpdateTaskStatus(request, CancellationToken.None);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
            Assert.False(apiResponse.Success);
            Assert.Equal("VALIDATION_ERROR", apiResponse.Error!.Code);
        }

        #endregion
    }
}
