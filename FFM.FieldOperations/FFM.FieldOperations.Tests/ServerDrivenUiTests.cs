using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFM.FieldOperations.Api.Controllers;
using FFM.FieldOperations.Application.DTOs;
using FFM.FieldOperations.Application.Handlers;
using FFM.FieldOperations.Application.Interfaces;
using FFM.FieldOperations.Application.Queries;
using FFM.FieldOperations.Domain.Entities;
using FFM.FieldOperations.Domain.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FFM.FieldOperations.Tests
{
    public class ServerDrivenUiTests
    {
        private readonly Mock<ITaskRepository> _taskRepoMock = new();
        private readonly Mock<IMediator> _mediatorMock = new();
        private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();

        public ServerDrivenUiTests()
        {
            _currentUserServiceMock.Setup(s => s.GetUserId()).Returns(10);
            _currentUserServiceMock.Setup(s => s.GetRoles()).Returns(new List<string> { "ADMIN" });
        }

        [Theory]
        [InlineData("AC_MAINTENANCE", "HVAC & AC Maintenance Evidence Checklist", 5)]
        [InlineData("SOLAR_PANEL", "Solar PV Array Servicing Evidence Checklist", 4)]
        [InlineData("ELEVATOR", "Elevator & Lift Maintenance Evidence Checklist", 4)]
        [InlineData("GENERAL", "General On-Site Service Evidence", 2)]
        [InlineData(null, "General On-Site Service Evidence", 2)]
        public async Task GetVisitEvidenceSchema_ReturnsCorrectChecklist_ForCategory(string? category, string expectedTitle, int expectedItemCount)
        {
            // Arrange
            var handler = new GetVisitEvidenceSchemaQueryHandler(_taskRepoMock.Object);
            var query = new GetVisitEvidenceSchemaQuery(category);

            // Act
            var result = await handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedTitle, result.Title);
            Assert.Equal(expectedItemCount, result.Items.Count);
        }

        [Fact]
        public async Task GetVisitEvidenceSchema_ResolvesCategoryFromTaskId()
        {
            // Arrange
            var task = new TaskItem(
                "TSK-202611-0099",
                "Chiller Overhaul",
                "Compressor check",
                "HIGH",
                1,
                10,
                DateTime.UtcNow.AddHours(1),
                DateTime.UtcNow.AddHours(3),
                1,
                "AC_MAINTENANCE"
            );
            _taskRepoMock.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(task);

            var handler = new GetVisitEvidenceSchemaQueryHandler(_taskRepoMock.Object);
            var query = new GetVisitEvidenceSchemaQuery(taskId: 99);

            // Act
            var result = await handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("AC_MAINTENANCE", result.TaskType);
            Assert.Contains(result.Items, i => i.Key == "compressorPressurePsi");
        }

        [Fact]
        public async Task GetTaskActions_ReturnsCorrectButtons_ForAssignedStatus()
        {
            // Arrange
            var task = new TaskItem(
                "TSK-202611-0010",
                "Daikin VRV System Check",
                "Check condenser",
                "HIGH",
                1,
                10,
                DateTime.UtcNow.AddHours(1),
                DateTime.UtcNow.AddHours(3),
                1,
                "AC_MAINTENANCE"
            );
            // Task status is ASSIGNED because AssignedEmployeeId = 10
            _taskRepoMock.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(task);

            var handler = new GetTaskActionsQueryHandler(_taskRepoMock.Object);
            var query = new GetTaskActionsQuery(10);

            // Act
            var result = await handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("ASSIGNED", result.CurrentStatus);
            Assert.Contains(result.Actions, a => a.ActionKey == "START_TRAVEL" && a.TargetEndpoint == "/api/v1/Tasks/status");
            Assert.Contains(result.Actions, a => a.ActionKey == "CANCEL_TASK");
        }

        [Fact]
        public async Task GetTaskActions_ReturnsCheckIn_ForEnRouteStatus()
        {
            // Arrange
            var task = new TaskItem(
                "TSK-202611-0011",
                "Elevator Cable Inspection",
                "Check cables",
                "URGENT",
                1,
                10,
                DateTime.UtcNow.AddHours(1),
                DateTime.UtcNow.AddHours(3),
                1,
                "ELEVATOR"
            );
            task.StartTravel(); // Moves status to EN_ROUTE
            _taskRepoMock.Setup(r => r.GetByIdAsync(11, It.IsAny<CancellationToken>())).ReturnsAsync(task);

            var handler = new GetTaskActionsQueryHandler(_taskRepoMock.Object);
            var query = new GetTaskActionsQuery(11);

            // Act
            var result = await handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("EN_ROUTE", result.CurrentStatus);
            Assert.Contains(result.Actions, a => a.ActionKey == "SITE_CHECK_IN" && a.TargetEndpoint == "/api/v1/Visits/site-check-in");
        }

        [Fact]
        public async Task GetTaskCancellationSchema_ReturnsStandardReasonList()
        {
            // Arrange
            var handler = new GetTaskCancellationSchemaQueryHandler();
            var query = new GetTaskCancellationSchemaQuery();

            // Act
            var result = await handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result.Reasons);
            Assert.Contains(result.Reasons, r => r.ReasonCode == "CLIENT_UNAVAILABLE");
            Assert.Contains(result.Reasons, r => r.ReasonCode == "SITE_INACCESSIBLE");
            Assert.Contains(result.Reasons, r => r.ReasonCode == "SPARE_PARTS_UNAVAILABLE");
        }

        [Fact]
        public async Task VisitsController_GetEvidenceSchema_Returns200Ok()
        {
            // Arrange
            var expectedDto = new TaskEvidenceSchemaDto { TaskType = "SOLAR_PANEL" };
            _mediatorMock.Setup(m => m.Send(It.IsAny<GetVisitEvidenceSchemaQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedDto);

            var controller = new VisitsController(_mediatorMock.Object, _currentUserServiceMock.Object);

            // Act
            var response = await controller.GetEvidenceSchema("SOLAR_PANEL", null, CancellationToken.None) as OkObjectResult;

            // Assert
            Assert.NotNull(response);
            Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        }

        [Fact]
        public async Task TasksController_GetTaskActions_Returns200Ok()
        {
            // Arrange
            var expectedDto = new TaskActionsDto { TaskId = 5, CurrentStatus = "ASSIGNED" };
            _mediatorMock.Setup(m => m.Send(It.IsAny<GetTaskActionsQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedDto);

            var controller = new TasksController(_mediatorMock.Object, _currentUserServiceMock.Object);

            // Act
            var response = await controller.GetTaskActions(5, CancellationToken.None) as OkObjectResult;

            // Assert
            Assert.NotNull(response);
            Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        }

        [Fact]
        public async Task TasksController_GetCancellationSchema_Returns200Ok()
        {
            // Arrange
            var expectedDto = new TaskCancellationSchemaDto();
            _mediatorMock.Setup(m => m.Send(It.IsAny<GetTaskCancellationSchemaQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedDto);

            var controller = new TasksController(_mediatorMock.Object, _currentUserServiceMock.Object);

            // Act
            var response = await controller.GetCancellationSchema(CancellationToken.None) as OkObjectResult;

            // Assert
            Assert.NotNull(response);
            Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        }

        [Theory]
        [InlineData("AC_MAINTENANCE", 6)]
        [InlineData("SOLAR_PANEL", 6)]
        [InlineData("ELEVATOR", 6)]
        [InlineData("GENERAL", 6)]
        public async Task GetTaskScreenLayout_ReturnsFullWidgetTree_ForTaskType(string taskType, int expectedWidgetCount)
        {
            // Arrange
            var handler = new GetTaskScreenLayoutQueryHandler();
            var query = new GetTaskScreenLayoutQuery(taskType: taskType);

            // Act
            var result = await handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("TASK_EXECUTION_SCREEN", result.ScreenId);
            Assert.Equal(taskType, result.TaskType);
            Assert.NotNull(result.Theme);
            Assert.Equal(expectedWidgetCount, result.Widgets.Count);
            Assert.Contains(result.Widgets, w => w.WidgetType == "HERO_HEADER");
            Assert.Contains(result.Widgets, w => w.WidgetType == "CLIENT_SITE_CARD");
            Assert.Contains(result.Widgets, w => w.WidgetType == "FORM_SECTION");
            Assert.Contains(result.Widgets, w => w.WidgetType == "TELEMETRY_GAUGES");
            Assert.Contains(result.Widgets, w => w.WidgetType == "EVIDENCE_GALLERY");
            Assert.Contains(result.Widgets, w => w.WidgetType == "BOTTOM_ACTION_BAR");
        }

        [Fact]
        public async Task GetTaskScreenLayout_HydratesLiveData_FromTaskId()
        {
            // Arrange
            var task = new TaskItem(
                "TSK-202611-0307",
                "Chiller Overhaul & Coil Test",
                "Compressor check",
                "HIGH",
                1,
                12,
                DateTime.UtcNow.AddHours(1),
                DateTime.UtcNow.AddHours(4),
                1,
                "AC_MAINTENANCE"
            );
            typeof(TaskItem).GetProperty("Id")!.SetValue(task, 307);

            var site = new ClientSite(
                "SITE-BLR-010",
                "HDFC Bank Regional Office",
                12.9715987m,
                77.5945627m,
                "No. 8/2, MG Road, Bengaluru",
                1,
                150,
                "Dr. Sharma",
                "+919880011223",
                "sharma@hdfc.com",
                "Bengaluru",
                "Karnataka",
                "560001"
            );
            typeof(ClientSite).GetProperty("Id")!.SetValue(site, 1);
            typeof(TaskItem).GetProperty("ClientSite")!.SetValue(task, site);

            _taskRepoMock.Setup(r => r.GetByIdAsync(307, It.IsAny<CancellationToken>())).ReturnsAsync(task);

            var clientSiteRepoMock = new Mock<IClientSiteRepository>();
            var handler = new GetTaskScreenLayoutQueryHandler(_taskRepoMock.Object, clientSiteRepoMock.Object, _mediatorMock.Object);
            var query = new GetTaskScreenLayoutQuery(taskId: 307);

            // Act
            var result = await handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(307, result.TaskId);
            Assert.Equal("TSK-202611-0307", result.TaskNumber);
            Assert.Equal("AC_MAINTENANCE", result.TaskType);

            var hero = result.Widgets.First(w => w.WidgetType == "HERO_HEADER");
            Assert.Equal("TSK-202611-0307", hero.Properties["taskNumber"]);
            Assert.Equal("HIGH", hero.Properties["priority"]);

            var siteCard = result.Widgets.First(w => w.WidgetType == "CLIENT_SITE_CARD");
            Assert.Equal("HDFC Bank Regional Office", siteCard.Properties["siteName"]);
            Assert.Equal(150, siteCard.Properties["radiusMeters"]);
        }

        [Fact]
        public async Task TasksController_GetTaskScreenLayout_Returns200Ok()
        {
            // Arrange
            var expectedDto = new ScreenLayoutDto { TaskId = 307, TaskType = "AC_MAINTENANCE" };
            _mediatorMock.Setup(m => m.Send(It.IsAny<GetTaskScreenLayoutQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedDto);

            var controller = new TasksController(_mediatorMock.Object, _currentUserServiceMock.Object);

            // Act
            var response = await controller.GetTaskScreenLayout(307, null, CancellationToken.None) as OkObjectResult;

            // Assert
            Assert.NotNull(response);
            Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
            var apiResponse = Assert.IsType<ApiResponse<ScreenLayoutDto>>(response.Value);
            Assert.True(apiResponse.Success);
            Assert.Equal(307, apiResponse.Data!.TaskId);
        }

        [Fact]
        public async Task GetTaskScreenLayout_HighSpeedCachedExecution_CompletesRapidly()
        {
            // Arrange
            var handler = new GetTaskScreenLayoutQueryHandler();
            var query = new GetTaskScreenLayoutQuery { TaskType = "AC_MAINTENANCE" };

            // Warm-up cache
            await handler.Handle(query, CancellationToken.None);

            // Act - measure 100 iterations
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 100; i++)
            {
                var layout = await handler.Handle(query, CancellationToken.None);
                Assert.NotNull(layout);
                Assert.Equal("AC_MAINTENANCE", layout.TaskType);
            }
            sw.Stop();

            // Assert - 100 in-memory requests should complete in well under 100ms total (<1ms per call)
            Assert.True(sw.ElapsedMilliseconds < 100, $"Expected 100 cached calls to finish in <100ms, took {sw.ElapsedMilliseconds}ms");
        }

        [Fact]
        public async Task GetTaskFormSchema_HighSpeedCachedExecution_CompletesRapidly()
        {
            // Arrange
            var handler = new GetTaskFormSchemaQueryHandler();
            var query = new GetTaskFormSchemaQuery("AC_MAINTENANCE");

            // Warm-up cache
            await handler.Handle(query, CancellationToken.None);

            // Act - measure 100 iterations
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 100; i++)
            {
                var schema = await handler.Handle(query, CancellationToken.None);
                Assert.NotNull(schema);
                Assert.Equal("AC_MAINTENANCE", schema.TaskType);
            }
            sw.Stop();

            // Assert - 100 in-memory requests should complete in well under 100ms total (<1ms per call)
            Assert.True(sw.ElapsedMilliseconds < 100, $"Expected 100 cached calls to finish in <100ms, took {sw.ElapsedMilliseconds}ms");
        }
    }
}

