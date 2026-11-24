using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFM.FieldOperations.Application.Commands;
using FFM.FieldOperations.Application.Handlers;
using FFM.FieldOperations.Application.Interfaces;
using FFM.FieldOperations.Domain.Entities;
using FFM.FieldOperations.Domain.Interfaces;
using Moq;
using Xunit;

namespace FFM.FieldOperations.Tests
{
    public class AddVisitEvidenceCommandHandlerTests
    {
        private readonly Mock<IVisitRepository> _visitRepoMock = new();
        private readonly Mock<IEvidenceTypeRepository> _evidenceTypeRepoMock = new();
        private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();

        private Visit CreateSampleVisit(int visitId = 501, int employeeId = 12, string status = "CHECKED_IN")
        {
            var visit = new Visit(
                taskId: 101,
                employeeId: employeeId,
                checkInTime: DateTime.UtcNow.AddMinutes(-30),
                checkInLatitude: 12.9715987m,
                checkInLongitude: 77.5945627m,
                checkInDistanceMeters: 5.5m,
                isWithinGeofence: true);

            // Reflection to set private Id for testing
            typeof(Visit).GetProperty("Id")!.SetValue(visit, visitId);
            typeof(Visit).GetProperty("Status")!.SetValue(visit, status);

            return visit;
        }

        private AddVisitEvidenceCommandHandler CreateHandler()
        {
            return new AddVisitEvidenceCommandHandler(
                _visitRepoMock.Object,
                _evidenceTypeRepoMock.Object,
                _currentUserServiceMock.Object);
        }

        [Fact]
        public async Task Handle_ValidPhotoEvidence_ByAssignedTechnician_ReturnsCreatedDto()
        {
            // Arrange
            var visit = CreateSampleVisit(501, employeeId: 12, status: "CHECKED_IN");
            _visitRepoMock.Setup(r => r.GetByIdAsync(501, It.IsAny<CancellationToken>()))
                .ReturnsAsync(visit);

            _visitRepoMock.Setup(r => r.AddEvidenceAsync(It.IsAny<VisitEvidence>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((VisitEvidence e, CancellationToken ct) =>
                {
                    typeof(VisitEvidence).GetProperty("Id")!.SetValue(e, 1001);
                    return e;
                });

            var handler = CreateHandler();
            var command = new AddVisitEvidenceCommand
            {
                VisitId = 501,
                EvidenceType = "PHOTO",
                FileUrl = "https://storage.ffm.example.com/evidence/photo_501.jpg",
                ThumbnailUrl = "https://storage.ffm.example.com/evidence/thumb_501.jpg",
                Remarks = "POS terminal repaired and verified.",
                CapturedLatitude = 12.9716000m,
                CapturedLongitude = 77.5945700m,
                ActorUserId = 12,
                ActorRoles = new List<string> { "EMPLOYEE" }
            };

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1001, result.Id);
            Assert.Equal(501, result.VisitId);
            Assert.Equal("PHOTO", result.EvidenceType);
            Assert.Equal("https://storage.ffm.example.com/evidence/photo_501.jpg", result.FileUrl);
            Assert.Equal("POS terminal repaired and verified.", result.Remarks);
            _visitRepoMock.Verify(r => r.AddEvidenceAsync(It.IsAny<VisitEvidence>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData("SIGNATURE")]
        [InlineData("DOCUMENT")]
        [InlineData("METER_READING")]
        [InlineData("INVOICE")]
        public async Task Handle_AllAllowedEvidenceTypes_Succeeds(string evidenceType)
        {
            // Arrange
            var visit = CreateSampleVisit(501, employeeId: 12, status: "CHECKED_IN");
            _visitRepoMock.Setup(r => r.GetByIdAsync(501, It.IsAny<CancellationToken>()))
                .ReturnsAsync(visit);

            _visitRepoMock.Setup(r => r.AddEvidenceAsync(It.IsAny<VisitEvidence>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((VisitEvidence e, CancellationToken ct) => e);

            var handler = CreateHandler();
            var command = new AddVisitEvidenceCommand
            {
                VisitId = 501,
                EvidenceType = evidenceType,
                FileUrl = "https://storage.ffm.example.com/evidence/doc.pdf",
                ActorUserId = 12,
                ActorRoles = new List<string> { "EMPLOYEE" }
            };

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(evidenceType, result.EvidenceType);
        }

        [Fact]
        public async Task Handle_ManagementUser_CanUploadEvidenceForAnyTechnician()
        {
            // Arrange
            var visit = CreateSampleVisit(501, employeeId: 12, status: "CHECKED_IN");
            _visitRepoMock.Setup(r => r.GetByIdAsync(501, It.IsAny<CancellationToken>()))
                .ReturnsAsync(visit);

            _visitRepoMock.Setup(r => r.AddEvidenceAsync(It.IsAny<VisitEvidence>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((VisitEvidence e, CancellationToken ct) => e);

            var handler = CreateHandler();
            var command = new AddVisitEvidenceCommand
            {
                VisitId = 501,
                EvidenceType = "DOCUMENT",
                FileUrl = "https://storage.ffm.example.com/reports/audit.pdf",
                ActorUserId = 999, // Manager ID
                ActorRoles = new List<string> { "MANAGER" }
            };

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("DOCUMENT", result.EvidenceType);
        }

        [Fact]
        public async Task Handle_InvalidVisitId_ThrowsArgumentException()
        {
            // Arrange
            var handler = CreateHandler();
            var command = new AddVisitEvidenceCommand
            {
                VisitId = 0,
                EvidenceType = "PHOTO",
                FileUrl = "https://storage.ffm.example.com/photo.jpg"
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(command, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_InvalidEvidenceType_ThrowsArgumentException()
        {
            // Arrange
            var handler = CreateHandler();
            var command = new AddVisitEvidenceCommand
            {
                VisitId = 501,
                EvidenceType = "VOICE_RECORDING",
                FileUrl = "https://storage.ffm.example.com/audio.mp3"
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("invalid", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("ftp://invalid.com/photo.jpg")]
        [InlineData("file:///C:/passwords.txt")]
        [InlineData("not-a-url")]
        public async Task Handle_InvalidFileUrl_ThrowsArgumentException(string invalidUrl)
        {
            // Arrange
            var handler = CreateHandler();
            var command = new AddVisitEvidenceCommand
            {
                VisitId = 501,
                EvidenceType = "PHOTO",
                FileUrl = invalidUrl
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(command, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_RemarksExceeding500Chars_ThrowsArgumentException()
        {
            // Arrange
            var handler = CreateHandler();
            var command = new AddVisitEvidenceCommand
            {
                VisitId = 501,
                EvidenceType = "PHOTO",
                FileUrl = "https://storage.ffm.example.com/photo.jpg",
                Remarks = new string('A', 501)
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("Remarks", ex.Message);
        }

        [Fact]
        public async Task Handle_LatitudeOutOfRange_ThrowsArgumentException()
        {
            // Arrange
            var handler = CreateHandler();
            var command = new AddVisitEvidenceCommand
            {
                VisitId = 501,
                EvidenceType = "PHOTO",
                FileUrl = "https://storage.ffm.example.com/photo.jpg",
                CapturedLatitude = 95.5m
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("CapturedLatitude", ex.Message);
        }

        [Fact]
        public async Task Handle_LongitudeOutOfRange_ThrowsArgumentException()
        {
            // Arrange
            var handler = CreateHandler();
            var command = new AddVisitEvidenceCommand
            {
                VisitId = 501,
                EvidenceType = "PHOTO",
                FileUrl = "https://storage.ffm.example.com/photo.jpg",
                CapturedLongitude = -185.0m
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("CapturedLongitude", ex.Message);
        }

        [Fact]
        public async Task Handle_VisitNotFound_ThrowsKeyNotFoundException()
        {
            // Arrange
            _visitRepoMock.Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Visit?)null);

            var handler = CreateHandler();
            var command = new AddVisitEvidenceCommand
            {
                VisitId = 999,
                EvidenceType = "PHOTO",
                FileUrl = "https://storage.ffm.example.com/photo.jpg",
                ActorUserId = 12,
                ActorRoles = new List<string> { "EMPLOYEE" }
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("999", ex.Message);
        }

        [Theory]
        [InlineData("ABORTED")]
        [InlineData("CANCELLED")]
        public async Task Handle_VisitInAbortedOrCancelledStatus_ThrowsInvalidOperationException(string invalidStatus)
        {
            // Arrange
            var visit = CreateSampleVisit(501, employeeId: 12, status: invalidStatus);
            _visitRepoMock.Setup(r => r.GetByIdAsync(501, It.IsAny<CancellationToken>()))
                .ReturnsAsync(visit);

            var handler = CreateHandler();
            var command = new AddVisitEvidenceCommand
            {
                VisitId = 501,
                EvidenceType = "PHOTO",
                FileUrl = "https://storage.ffm.example.com/photo.jpg",
                ActorUserId = 12,
                ActorRoles = new List<string> { "EMPLOYEE" }
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains(invalidStatus, ex.Message);
        }

        [Fact]
        public async Task Handle_TechnicianMismatch_BOLA_ThrowsUnauthorizedAccessException()
        {
            // Arrange: Visit belongs to technician 12, but caller is technician 99
            var visit = CreateSampleVisit(501, employeeId: 12, status: "CHECKED_IN");
            _visitRepoMock.Setup(r => r.GetByIdAsync(501, It.IsAny<CancellationToken>()))
                .ReturnsAsync(visit);

            var handler = CreateHandler();
            var command = new AddVisitEvidenceCommand
            {
                VisitId = 501,
                EvidenceType = "PHOTO",
                FileUrl = "https://storage.ffm.example.com/photo.jpg",
                ActorUserId = 99, // Unauthorized technician
                ActorRoles = new List<string> { "EMPLOYEE" }
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("Access denied", ex.Message);
        }
    }
}

