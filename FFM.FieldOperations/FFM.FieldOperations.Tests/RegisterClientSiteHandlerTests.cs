using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFM.FieldOperations.Application.Commands;
using FFM.FieldOperations.Application.Handlers;
using FFM.FieldOperations.Application.Interfaces;
using FFM.FieldOperations.Domain.Entities;
using Moq;
using Xunit;

namespace FFM.FieldOperations.Tests
{
    public class RegisterClientSiteHandlerTests
    {
        private readonly Mock<IClientSiteRepository> _repoMock = new();
        private readonly Mock<ICurrentUserService> _userMock = new();

        public RegisterClientSiteHandlerTests()
        {
            // Default: authenticated user with MANAGER role
            _userMock.Setup(u => u.GetRoles()).Returns(new List<string> { "MANAGER" });
            _userMock.Setup(u => u.GetUserId()).Returns(501);
        }

        [Fact]
        public async Task RegisterClientSite_ValidPayload_ReturnsCreatedSiteDto()
        {
            // Arrange
            _repoMock.Setup(r => r.ExistsByCodeAsync("SITE-BLR-001")).ReturnsAsync(false);

            var handler = new CreateClientSiteCommandHandler(_repoMock.Object, _userMock.Object);
            var command = new CreateClientSiteCommand
            {
                ActorUserId = 501,
                SiteCode = "SITE-BLR-001",
                Name = "HDFC Bank Regional Office",
                Latitude = 12.9715987m,
                Longitude = 77.5945627m,
                RadiusMeters = 150,
                Address = "42 MG Road, Bengaluru, Karnataka",
                ContactPerson = "Ramesh Kumar",
                ContactPhone = "+919876543210"
            };

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("SITE-BLR-001", result.SiteCode);
            Assert.Equal("HDFC Bank Regional Office", result.Name);
            Assert.Equal(12.9715987m, result.Latitude);
            Assert.Equal(77.5945627m, result.Longitude);
            Assert.Equal(150, result.RadiusMeters);
            Assert.True(result.IsActive);
            _repoMock.Verify(r => r.AddAsync(It.IsAny<ClientSite>()), Times.Once);
            _repoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task RegisterClientSite_DuplicateCode_ThrowsInvalidOperationException()
        {
            // Arrange
            _repoMock.Setup(r => r.ExistsByCodeAsync("SITE-BLR-001")).ReturnsAsync(true);

            var handler = new CreateClientSiteCommandHandler(_repoMock.Object, _userMock.Object);
            var command = new CreateClientSiteCommand
            {
                ActorUserId = 501,
                SiteCode = "SITE-BLR-001",
                Name = "Another Bank",
                Latitude = 12.9715987m,
                Longitude = 77.5945627m,
                Address = "42 MG Road, Bengaluru"
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("already exists", ex.Message);
        }

        [Fact]
        public async Task RegisterClientSite_LatitudeOutOfRange_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            _repoMock.Setup(r => r.ExistsByCodeAsync(It.IsAny<string>())).ReturnsAsync(false);

            var handler = new CreateClientSiteCommandHandler(_repoMock.Object, _userMock.Object);
            var command = new CreateClientSiteCommand
            {
                ActorUserId = 501,
                SiteCode = "SITE-TEST-002",
                Name = "Test Site",
                Latitude = 95.0000000m, // > 90
                Longitude = 77.5945627m,
                Address = "42 MG Road, Bengaluru"
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => handler.Handle(command, CancellationToken.None));
        }

        [Fact]
        public async Task RegisterClientSite_LongitudeOutOfRange_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            _repoMock.Setup(r => r.ExistsByCodeAsync(It.IsAny<string>())).ReturnsAsync(false);

            var handler = new CreateClientSiteCommandHandler(_repoMock.Object, _userMock.Object);
            var command = new CreateClientSiteCommand
            {
                ActorUserId = 501,
                SiteCode = "SITE-TEST-003",
                Name = "Test Site",
                Latitude = 12.9715987m,
                Longitude = -185.0000000m, // < -180
                Address = "42 MG Road, Bengaluru"
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => handler.Handle(command, CancellationToken.None));
        }

        [Fact]
        public async Task RegisterClientSite_NullIslandCoordinates_ThrowsArgumentException()
        {
            // Arrange
            _repoMock.Setup(r => r.ExistsByCodeAsync(It.IsAny<string>())).ReturnsAsync(false);

            var handler = new CreateClientSiteCommandHandler(_repoMock.Object, _userMock.Object);
            var command = new CreateClientSiteCommand
            {
                ActorUserId = 501,
                SiteCode = "SITE-TEST-004",
                Name = "Test Site",
                Latitude = 0.0m,
                Longitude = 0.0m,
                Address = "42 MG Road, Bengaluru"
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("0.0, 0.0", ex.Message);
        }

        [Fact]
        public async Task RegisterClientSite_RadiusLessThan20m_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            _repoMock.Setup(r => r.ExistsByCodeAsync(It.IsAny<string>())).ReturnsAsync(false);

            var handler = new CreateClientSiteCommandHandler(_repoMock.Object, _userMock.Object);
            var command = new CreateClientSiteCommand
            {
                ActorUserId = 501,
                SiteCode = "SITE-TEST-005",
                Name = "Test Site",
                Latitude = 12.9715987m,
                Longitude = 77.5945627m,
                RadiusMeters = 10, // < 20m
                Address = "42 MG Road, Bengaluru"
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => handler.Handle(command, CancellationToken.None));
        }

        [Fact]
        public async Task RegisterClientSite_ShortAddress_ThrowsArgumentException()
        {
            // Arrange
            _repoMock.Setup(r => r.ExistsByCodeAsync(It.IsAny<string>())).ReturnsAsync(false);

            var handler = new CreateClientSiteCommandHandler(_repoMock.Object, _userMock.Object);
            var command = new CreateClientSiteCommand
            {
                ActorUserId = 501,
                SiteCode = "SITE-TEST-006",
                Name = "Test Site",
                Latitude = 12.9715987m,
                Longitude = 77.5945627m,
                Address = "Short" // < 10 chars
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(command, CancellationToken.None));
        }

        [Fact]
        public async Task RegisterClientSite_NonManagerNonAdmin_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            _userMock.Setup(u => u.GetRoles()).Returns(new List<string> { "EMPLOYEE" });

            var handler = new CreateClientSiteCommandHandler(_repoMock.Object, _userMock.Object);
            var command = new CreateClientSiteCommand
            {
                ActorUserId = 101,
                SiteCode = "SITE-TEST-007",
                Name = "Test Site",
                Latitude = 12.9715987m,
                Longitude = 77.5945627m,
                Address = "42 MG Road, Bengaluru"
            };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(command, CancellationToken.None));
        }
    }
}
