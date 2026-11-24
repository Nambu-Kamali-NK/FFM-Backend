using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFM.FieldOperations.Application.Commands;
using FFM.FieldOperations.Application.Handlers;
using FFM.FieldOperations.Domain.Entities;
using FFM.FieldOperations.Domain.Interfaces;
using Moq;
using Xunit;

namespace FFM.FieldOperations.Tests
{
    public class CheckOutVisitCommandHandlerTests
    {
        private readonly Mock<IVisitRepository> _visitRepoMock = new();
        private readonly Mock<ITaskRepository> _taskRepoMock = new();

        private CheckOutVisitCommandHandler CreateHandler()
        {
            return new CheckOutVisitCommandHandler(
                _visitRepoMock.Object,
                _taskRepoMock.Object);
        }

        private (Visit visit, TaskItem task) CreateTestCheckedInVisitWithEvidence(
            int visitId = 10,
            int taskId = 5,
            int employeeId = 8,
            bool attachEvidence = true,
            string taskStatus = "CHECKED_IN")
        {
            var task = new TaskItem(
                taskNumber: $"TSK-TEST-{taskId:D4}",
                title: "ATM POS Sensor Calibration",
                description: "Calibrate optical sensor on cash dispenser.",
                priority: "HIGH",
                clientSiteId: 1,
                assignedEmployeeId: employeeId,
                scheduledStart: DateTime.UtcNow.AddHours(-2),
                scheduledEnd: DateTime.UtcNow.AddHours(2),
                createdByUserId: 1);

            typeof(TaskItem).GetProperty("Id")?.SetValue(task, taskId);

            if (taskStatus == "CHECKED_IN")
            {
                task.CheckIn();
            }

            var checkInTime = DateTime.UtcNow.AddMinutes(-45);
            var visit = new Visit(
                taskId: taskId,
                employeeId: employeeId,
                checkInTime: checkInTime,
                checkInLatitude: 12.9715987m,
                checkInLongitude: 77.5945627m,
                checkInDistanceMeters: 10m,
                isWithinGeofence: true);

            typeof(Visit).GetProperty("Id")?.SetValue(visit, visitId);
            typeof(Visit).GetProperty("Task")?.SetValue(visit, task);

            if (attachEvidence)
            {
                var evidence = new VisitEvidence(
                    visitId: visitId,
                    evidenceType: "PHOTO",
                    fileUrl: "https://storage.ffm.example.com/evidence/work_done.jpg",
                    thumbnailUrl: "https://storage.ffm.example.com/evidence/thumb_work_done.jpg",
                    remarks: "Completed sensor calibration photo",
                    capturedLatitude: 12.9715987m,
                    capturedLongitude: 77.5945627m);
                visit.AddEvidence(evidence);
            }

            return (visit, task);
        }

        [Fact]
        public async Task Handle_ValidCheckOut_Returns200WithDurationAndCompletedStatus()
        {
            // Arrange
            var (visit, task) = CreateTestCheckedInVisitWithEvidence(visitId: 10, employeeId: 8);

            _visitRepoMock.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(visit);

            var command = new CheckOutVisitCommand
            {
                VisitId = 10,
                Latitude = 12.9715987m,
                Longitude = 77.5945627m,
                CompletionNotes = "Replaced faulty biometric scanner module and tested line pressure. All tests passed.",
                CustomerName = "Ramesh Kumar",
                CustomerSignatureUrl = "https://storage.ffm.example.com/signatures/sig_10.png",
                CustomerRating = 5,
                ActorUserId = 8,
                ActorRoles = new[] { "EMPLOYEE" }
            };

            var handler = CreateHandler();

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(10, result.VisitId);
            Assert.Equal("COMPLETED", result.TaskStatus);
            Assert.True(result.DurationMinutes >= 44m);
            Assert.Equal("Ramesh Kumar", result.CustomerName);
            Assert.Equal(5, result.CustomerRating);
            Assert.Equal("COMPLETED", visit.Status);
            Assert.Equal("COMPLETED", task.Status);

            _visitRepoMock.Verify(r => r.UpdateVisitAsync(visit, It.IsAny<CancellationToken>()), Times.Once);
            _taskRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_NonExistentVisit_ThrowsKeyNotFoundException()
        {
            // Arrange
            _visitRepoMock.Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Visit?)null);

            var command = new CheckOutVisitCommand
            {
                VisitId = 999,
                Latitude = 12.9715987m,
                Longitude = 77.5945627m,
                CompletionNotes = "Replaced parts.",
                ActorUserId = 8,
                ActorRoles = new[] { "EMPLOYEE" }
            };

            var handler = CreateHandler();

            // Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(command, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_AlreadyCheckedOutVisit_ThrowsInvalidOperationException()
        {
            // Arrange
            var (visit, _) = CreateTestCheckedInVisitWithEvidence(visitId: 10, employeeId: 8);
            visit.Complete(
                checkOutTime: DateTime.UtcNow,
                checkOutLatitude: 12.9715987m,
                checkOutLongitude: 77.5945627m,
                completionNotes: "Previous checkout notes",
                customerName: "Jane Doe",
                customerSignatureUrl: null);

            _visitRepoMock.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(visit);

            var command = new CheckOutVisitCommand
            {
                VisitId = 10,
                Latitude = 12.9715987m,
                Longitude = 77.5945627m,
                CompletionNotes = "Attempting duplicate checkout.",
                ActorUserId = 8,
                ActorRoles = new[] { "EMPLOYEE" }
            };

            var handler = CreateHandler();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("already been checked out", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Handle_BOLA_UnauthorizedTechnician_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            var (visit, _) = CreateTestCheckedInVisitWithEvidence(visitId: 10, employeeId: 8);

            _visitRepoMock.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(visit);

            var command = new CheckOutVisitCommand
            {
                VisitId = 10,
                Latitude = 12.9715987m,
                Longitude = 77.5945627m,
                CompletionNotes = "Cross technician check out attempt.",
                ActorUserId = 99, // Different employee!
                ActorRoles = new[] { "EMPLOYEE" }
            };

            var handler = CreateHandler();

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(command, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_SupervisorCheckingOutOnBehalfOfTechnician_Succeeds()
        {
            // Arrange
            var (visit, _) = CreateTestCheckedInVisitWithEvidence(visitId: 10, employeeId: 8);

            _visitRepoMock.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(visit);

            var command = new CheckOutVisitCommand
            {
                VisitId = 10,
                Latitude = 12.9715987m,
                Longitude = 77.5945627m,
                CompletionNotes = "Supervisor closing visit on behalf of field tech.",
                ActorUserId = 11, // Supervisor
                ActorRoles = new[] { "SUPERVISOR" }
            };

            var handler = CreateHandler();

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("COMPLETED", result.TaskStatus);
        }

        [Fact]
        public async Task Handle_MissingEvidence_ThrowsInvalidOperationException()
        {
            // Arrange (attachEvidence = false -> BR-FLD-05)
            var (visit, _) = CreateTestCheckedInVisitWithEvidence(visitId: 10, employeeId: 8, attachEvidence: false);

            _visitRepoMock.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(visit);

            var command = new CheckOutVisitCommand
            {
                VisitId = 10,
                Latitude = 12.9715987m,
                Longitude = 77.5945627m,
                CompletionNotes = "Closing without any photo evidence attached.",
                ActorUserId = 8,
                ActorRoles = new[] { "EMPLOYEE" }
            };

            var handler = CreateHandler();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("evidence is required", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Handle_ShortCompletionNotes_ThrowsArgumentException()
        {
            // Arrange
            var command = new CheckOutVisitCommand
            {
                VisitId = 10,
                Latitude = 12.9715987m,
                Longitude = 77.5945627m,
                CompletionNotes = "Done", // Less than 5 chars!
                ActorUserId = 8,
                ActorRoles = new[] { "EMPLOYEE" }
            };

            var handler = CreateHandler();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(command, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_InvalidCustomerRating_ThrowsArgumentException()
        {
            // Arrange
            var command = new CheckOutVisitCommand
            {
                VisitId = 10,
                Latitude = 12.9715987m,
                Longitude = 77.5945627m,
                CompletionNotes = "Valid resolution notes provided.",
                CustomerRating = 6, // Invalid (>5)
                ActorUserId = 8,
                ActorRoles = new[] { "EMPLOYEE" }
            };

            var handler = CreateHandler();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(command, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_InvalidCoordinates_ThrowsArgumentException()
        {
            // Arrange
            var command = new CheckOutVisitCommand
            {
                VisitId = 10,
                Latitude = 95.0m, // Invalid (>90)
                Longitude = 77.5945627m,
                CompletionNotes = "Valid resolution notes provided.",
                ActorUserId = 8,
                ActorRoles = new[] { "EMPLOYEE" }
            };

            var handler = CreateHandler();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(command, CancellationToken.None));
        }
    }
}

