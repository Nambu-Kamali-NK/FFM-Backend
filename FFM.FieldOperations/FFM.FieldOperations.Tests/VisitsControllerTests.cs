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
    public class VisitsControllerTests
    {
        private readonly Mock<IMediator> _mediatorMock = new();
        private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
        private readonly Mock<IS3StorageService> _s3StorageServiceMock = new();

        private VisitsController CreateController()
        {
            _currentUserServiceMock.Setup(u => u.GetUserId()).Returns(12);
            _currentUserServiceMock.Setup(u => u.GetRoles()).Returns(new List<string> { "EMPLOYEE" });

            return new VisitsController(
                _mediatorMock.Object,
                _currentUserServiceMock.Object,
                _s3StorageServiceMock.Object);
        }

        [Fact]
        public async Task AddEvidence_NullRequest_ReturnsBadRequest()
        {
            // Arrange
            var controller = CreateController();

            // Act
            var result = await controller.AddEvidence(null!, CancellationToken.None);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        }

        [Fact]
        public async Task AddEvidence_ValidPayload_Returns201Created()
        {
            // Arrange
            var controller = CreateController();
            var expectedDto = new VisitEvidenceDto
            {
                Id = 1001,
                VisitId = 501,
                EvidenceType = "PHOTO",
                FileUrl = "https://storage.ffm.example.com/photo.jpg",
                CapturedAt = DateTime.UtcNow
            };

            _mediatorMock.Setup(m => m.Send(It.IsAny<AddVisitEvidenceCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedDto);

            var request = new AddVisitEvidenceRequest
            {
                VisitId = 501,
                EvidenceTypeId = 1,
                FileUrls = new List<string> { "https://storage.ffm.example.com/photo.jpg" }
            };

            // Act
            var result = await controller.AddEvidence(request, CancellationToken.None);

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status201Created, objectResult.StatusCode);
            var apiResponse = Assert.IsType<ApiResponse<VisitEvidenceDto>>(objectResult.Value);
            Assert.True(apiResponse.Success);
            Assert.Equal(1001, apiResponse.Data?.Id);
        }

        [Fact]
        public async Task AddEvidence_VisitNotFound_Returns404NotFound()
        {
            // Arrange
            var controller = CreateController();
            _mediatorMock.Setup(m => m.Send(It.IsAny<AddVisitEvidenceCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new KeyNotFoundException("Visit not found"));

            var request = new AddVisitEvidenceRequest
            {
                VisitId = 999,
                EvidenceTypeId = 1,
                FileUrls = new List<string> { "https://storage.ffm.example.com/photo.jpg" }
            };

            // Act
            var result = await controller.AddEvidence(request, CancellationToken.None);

            // Assert
            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
        }

        [Fact]
        public async Task AddEvidence_BOLAUnauthorized_Returns403Forbidden()
        {
            // Arrange
            var controller = CreateController();
            _mediatorMock.Setup(m => m.Send(It.IsAny<AddVisitEvidenceCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new UnauthorizedAccessException("Access denied"));

            var request = new AddVisitEvidenceRequest
            {
                VisitId = 501,
                EvidenceTypeId = 1,
                FileUrls = new List<string> { "https://storage.ffm.example.com/photo.jpg" }
            };

            // Act
            var result = await controller.AddEvidence(request, CancellationToken.None);

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        }

        [Fact]
        public async Task AddEvidence_InvalidOperation_Returns400BadRequest()
        {
            // Arrange
            var controller = CreateController();
            _mediatorMock.Setup(m => m.Send(It.IsAny<AddVisitEvidenceCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Visit is aborted"));

            var request = new AddVisitEvidenceRequest
            {
                VisitId = 501,
                EvidenceTypeId = 1,
                FileUrls = new List<string> { "https://storage.ffm.example.com/photo.jpg" }
            };

            // Act
            var result = await controller.AddEvidence(request, CancellationToken.None);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        }

        [Fact]
        public async Task GetEvidences_ValidQuery_Returns200OK()
        {
            // Arrange
            var controller = CreateController();
            var list = new List<VisitEvidenceDto>
            {
                new VisitEvidenceDto { Id = 1001, VisitId = 501, EvidenceType = "PHOTO", FileUrl = "https://storage.com/1.jpg" }
            };

            _mediatorMock.Setup(m => m.Send(It.IsAny<GetVisitEvidencesQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(list);

            // Act
            var result = await controller.GetEvidences(501, null, null, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
            var apiResponse = Assert.IsType<ApiResponse<IReadOnlyList<VisitEvidenceDto>>>(okResult.Value);
            Assert.True(apiResponse.Success);
            Assert.Single(apiResponse.Data!);
        }

        [Fact]
        public async Task GetEvidences_MissingFilters_Returns400BadRequest()
        {
            // Arrange
            var controller = CreateController();
            _mediatorMock.Setup(m => m.Send(It.IsAny<GetVisitEvidencesQuery>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Either VisitId or TaskId must be specified"));

            // Act
            var result = await controller.GetEvidences(null, null, null, CancellationToken.None);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        }

        [Fact]
        public async Task CheckIn_NullRequest_ReturnsBadRequest()
        {
            // Arrange
            var controller = CreateController();

            // Act
            var result = await controller.CheckIn(null!, CancellationToken.None);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        }

        [Fact]
        public async Task CheckIn_ValidPayload_Returns201Created()
        {
            // Arrange
            var controller = CreateController();
            var expectedDto = new VisitCheckInResultDto
            {
                VisitId = 501,
                TaskId = 102,
                TaskNumber = "TSK-TEST-0102",
                EmployeeId = 12,
                CheckInTime = DateTime.UtcNow,
                CheckInLatitude = 12.97161m,
                CheckInLongitude = 77.59457m,
                DistanceMeters = 14.25m,
                AllowedRadiusMeters = 150,
                IsWithinGeofence = true,
                Status = "CHECKED_IN",
                IsIdempotentReplay = false
            };

            _mediatorMock.Setup(m => m.Send(It.IsAny<CheckInVisitCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedDto);

            var request = new VisitCheckInRequest
            {
                TaskId = 102,
                Latitude = 12.97161m,
                Longitude = 77.59457m
            };

            // Act
            var result = await controller.CheckIn(request, CancellationToken.None);

            // Assert
            var createdResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status201Created, createdResult.StatusCode);
            var apiResponse = Assert.IsType<ApiResponse<VisitCheckInResultDto>>(createdResult.Value);
            Assert.True(apiResponse.Success);
            Assert.Equal(501, apiResponse.Data!.VisitId);
        }

        [Fact]
        public async Task CheckIn_IdempotentRetry_Returns200OK()
        {
            // Arrange
            var controller = CreateController();
            var expectedDto = new VisitCheckInResultDto
            {
                VisitId = 501,
                TaskId = 102,
                TaskNumber = "TSK-TEST-0102",
                EmployeeId = 12,
                CheckInTime = DateTime.UtcNow,
                CheckInLatitude = 12.97161m,
                CheckInLongitude = 77.59457m,
                DistanceMeters = 14.25m,
                AllowedRadiusMeters = 150,
                IsWithinGeofence = true,
                Status = "CHECKED_IN",
                IsIdempotentReplay = true
            };

            _mediatorMock.Setup(m => m.Send(It.IsAny<CheckInVisitCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedDto);

            var request = new VisitCheckInRequest
            {
                TaskId = 102,
                Latitude = 12.97161m,
                Longitude = 77.59457m
            };

            // Act
            var result = await controller.CheckIn(request, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
            var apiResponse = Assert.IsType<ApiResponse<VisitCheckInResultDto>>(okResult.Value);
            Assert.True(apiResponse.Success);
            Assert.True(apiResponse.Data!.IsIdempotentReplay);
        }

        [Fact]
        public async Task CheckIn_TaskNotFound_Returns404NotFound()
        {
            // Arrange
            var controller = CreateController();
            _mediatorMock.Setup(m => m.Send(It.IsAny<CheckInVisitCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new KeyNotFoundException("Task with ID 999 was not found"));

            var request = new VisitCheckInRequest { TaskId = 999, Latitude = 12m, Longitude = 77m };

            // Act
            var result = await controller.CheckIn(request, CancellationToken.None);

            // Assert
            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
        }

        [Fact]
        public async Task CheckIn_BOLA_Returns403Forbidden()
        {
            // Arrange
            var controller = CreateController();
            _mediatorMock.Setup(m => m.Send(It.IsAny<CheckInVisitCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new UnauthorizedAccessException("Access denied"));

            var request = new VisitCheckInRequest { TaskId = 102, Latitude = 12m, Longitude = 77m };

            // Act
            var result = await controller.CheckIn(request, CancellationToken.None);

            // Assert
            var forbidden = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        }

        [Fact]
        public async Task CheckIn_OutsideGeofence_Returns400BadRequest()
        {
            // Arrange
            var controller = CreateController();
            _mediatorMock.Setup(m => m.Send(It.IsAny<CheckInVisitCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("You are 1,450.50 meters away from the client site"));

            var request = new VisitCheckInRequest { TaskId = 102, Latitude = 12m, Longitude = 77m };

            // Act
            var result = await controller.CheckIn(request, CancellationToken.None);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
            var apiResponse = Assert.IsType<ApiResponse<object>>(badRequest.Value);
            Assert.Equal("OUTSIDE_GEOFENCE_BOUNDARY", apiResponse.Error!.Code);
        }

        [Fact]
        public async Task CheckIn_ActiveVisitExists_Returns409Conflict()
        {
            // Arrange
            var controller = CreateController();
            _mediatorMock.Setup(m => m.Send(It.IsAny<CheckInVisitCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Technician already has an active checked-in visit"));

            var request = new VisitCheckInRequest { TaskId = 102, Latitude = 12m, Longitude = 77m };

            // Act
            var result = await controller.CheckIn(request, CancellationToken.None);

            // Assert
            var conflict = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
            var apiResponse = Assert.IsType<ApiResponse<object>>(conflict.Value);
            Assert.Equal("ACTIVE_VISIT_EXISTS", apiResponse.Error!.Code);
        }

        [Fact]
        public async Task CheckOut_NullRequest_Returns400BadRequest()
        {
            // Arrange
            var controller = CreateController();

            // Act
            var result = await controller.CheckOut(null!, CancellationToken.None);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        }

        [Fact]
        public async Task CheckOut_ValidPayload_Returns200OK()
        {
            // Arrange
            var controller = CreateController();
            var expectedDto = new VisitCheckOutResultDto
            {
                VisitId = 501,
                TaskId = 102,
                TaskNumber = "TSK-TEST-0102",
                CheckInTime = DateTime.UtcNow.AddHours(-1),
                CheckOutTime = DateTime.UtcNow,
                DurationMinutes = 60.0m,
                TaskStatus = "COMPLETED",
                CompletionNotes = "Resolution notes provided",
                CustomerRating = 5
            };

            _mediatorMock.Setup(m => m.Send(It.IsAny<CheckOutVisitCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedDto);

            var request = new VisitCheckOutRequest
            {
                VisitId = 501,
                Latitude = 12.9715987m,
                Longitude = 77.5945627m,
                CompletionNotes = "Resolution notes provided",
                CustomerRating = 5
            };

            // Act
            var result = await controller.CheckOut(request, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        }

        [Fact]
        public async Task CheckOut_VisitNotFound_Returns404NotFound()
        {
            // Arrange
            var controller = CreateController();
            _mediatorMock.Setup(m => m.Send(It.IsAny<CheckOutVisitCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new KeyNotFoundException("Visit not found"));

            var request = new VisitCheckOutRequest { VisitId = 999, CompletionNotes = "Notes" };

            // Act
            var result = await controller.CheckOut(request, CancellationToken.None);

            // Assert
            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
        }

        [Fact]
        public async Task CheckOut_BOLA_Returns403Forbidden()
        {
            // Arrange
            var controller = CreateController();
            _mediatorMock.Setup(m => m.Send(It.IsAny<CheckOutVisitCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new UnauthorizedAccessException("Access denied"));

            var request = new VisitCheckOutRequest { VisitId = 501, CompletionNotes = "Notes" };

            // Act
            var result = await controller.CheckOut(request, CancellationToken.None);

            // Assert
            var forbidden = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        }

        [Fact]
        public async Task CheckOut_MissingEvidence_Returns400BadRequest()
        {
            // Arrange
            var controller = CreateController();
            _mediatorMock.Setup(m => m.Send(It.IsAny<CheckOutVisitCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("At least one proof of visit evidence is required"));

            var request = new VisitCheckOutRequest { VisitId = 501, CompletionNotes = "Notes" };

            // Act
            var result = await controller.CheckOut(request, CancellationToken.None);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
            var apiResponse = Assert.IsType<ApiResponse<object>>(badRequest.Value);
            Assert.Equal("MANDATORY_EVIDENCE_REQUIRED", apiResponse.Error!.Code);
        }

        [Fact]
        public async Task AddEvidence_WithStructuredFilesList_Returns201Created()
        {
            // Arrange
            var controller = CreateController();
            var expectedDto = new VisitEvidenceDto
            {
                Id = 1002,
                VisitId = 501,
                FileName = "ac-repair.jpg",
                FolderPath = "tasks/307/evidence",
                FileUrl = "https://s3.amazonaws.com/tasks/307/evidence/ac-repair.jpg",
                EvidenceTypeId = 1,
                EvidenceType = "PHOTO",
                CapturedAt = DateTime.UtcNow
            };

            _mediatorMock.Setup(m => m.Send(It.IsAny<AddVisitEvidenceCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedDto);

            var request = new AddVisitEvidenceRequest
            {
                VisitId = 501,
                Files = new List<EvidenceFileItemDto>
                {
                    new EvidenceFileItemDto
                    {
                        FileName = "ac-repair.jpg",
                        FolderPath = "tasks/307/evidence",
                        FileUrl = "https://s3.amazonaws.com/tasks/307/evidence/ac-repair.jpg",
                        EvidenceTypeId = 1
                    },
                    new EvidenceFileItemDto
                    {
                        FileName = "meter-reading.jpg",
                        FolderPath = "tasks/307/evidence",
                        FileUrl = "https://s3.amazonaws.com/tasks/307/evidence/meter-reading.jpg",
                        EvidenceTypeId = 2
                    }
                },
                Remarks = "Two photos with individual types"
            };

            // Act
            var result = await controller.AddEvidence(request, CancellationToken.None);

            // Assert
            var created = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
            var apiResponse = Assert.IsType<ApiResponse<VisitEvidenceDto>>(created.Value);
            Assert.True(apiResponse.Success);
            Assert.Equal("ac-repair.jpg", apiResponse.Data!.FileName);
            Assert.Equal("tasks/307/evidence", apiResponse.Data!.FolderPath);
        }

        [Fact]
        public async Task AddEvidence_WithRootFileNameAndFolderPath_Returns201Created()
        {
            // Arrange
            var controller = CreateController();
            var expectedDto = new VisitEvidenceDto
            {
                Id = 1003,
                VisitId = 501,
                FileName = "direct-upload.jpg",
                FolderPath = "visits/501",
                FileUrl = "https://s3.amazonaws.com/visits/501/direct-upload.jpg",
                EvidenceTypeId = 1,
                EvidenceType = "PHOTO",
                CapturedAt = DateTime.UtcNow
            };

            _mediatorMock.Setup(m => m.Send(It.IsAny<AddVisitEvidenceCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedDto);

            var request = new AddVisitEvidenceRequest
            {
                VisitId = 501,
                FileName = "direct-upload.jpg",
                FolderPath = "visits/501",
                FileUrls = new List<string> { "https://s3.amazonaws.com/visits/501/direct-upload.jpg" },
                EvidenceTypeId = 1
            };

            // Act
            var result = await controller.AddEvidence(request, CancellationToken.None);

            // Assert
            var created = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
            var apiResponse = Assert.IsType<ApiResponse<VisitEvidenceDto>>(created.Value);
            Assert.Equal("direct-upload.jpg", apiResponse.Data!.FileName);
            Assert.Equal("visits/501", apiResponse.Data!.FolderPath);
        }
    }
}

