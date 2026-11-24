using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFM.FieldOperations.Api.Controllers;
using FFM.FieldOperations.Application.Commands;
using FFM.FieldOperations.Application.DTOs;
using FFM.FieldOperations.Application.Interfaces;
using FFM.FieldOperations.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FFM.FieldOperations.Tests
{
    public class ClientSitesControllerTests
    {
        private readonly Mock<IMediator> _mediatorMock = new();
        private readonly Mock<IClientSiteRepository> _repoMock = new();
        private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
        private readonly ClientSitesController _controller;

        public ClientSitesControllerTests()
        {
            _currentUserServiceMock.Setup(s => s.GetUserId()).Returns(12);
            _currentUserServiceMock.Setup(s => s.GetRoles()).Returns(new List<string> { "ADMIN" });

            _controller = new ClientSitesController(_mediatorMock.Object, _repoMock.Object, _currentUserServiceMock.Object);
        }

        private ClientSite CreateDomainSite(int id = 1, string siteCode = "SITE-BLR-001", bool active = true)
        {
            var site = new ClientSite(
                siteCode,
                "HDFC Bank Regional Office",
                12.9715987m,
                77.5945627m,
                "42 MG Road, Bengaluru, Karnataka",
                12,
                150,
                "Ramesh Kumar",
                "+919876543210",
                "ramesh@hdfcbank.com",
                "Bengaluru",
                "Karnataka",
                "560001");

            if (!active)
            {
                site.Deactivate();
            }
            return site;
        }

        #region 1. RegisterClientSite API Tests (POST /api/v1/ClientSites)

        // --- Positive Cases ---

        [Fact]
        public async Task RegisterClientSite_ValidPayload_Returns201CreatedWithDto()
        {
            // Arrange
            var request = new CreateClientSiteRequest
            {
                SiteCode = "SITE-BLR-001",
                Name = "HDFC Bank Regional Office",
                Latitude = 12.9715987m,
                Longitude = 77.5945627m,
                RadiusMeters = 150,
                Address = "42 MG Road, Bengaluru, Karnataka",
                ContactPerson = "Ramesh Kumar",
                ContactPhone = "+919876543210",
                ContactEmail = "ramesh@hdfcbank.com"
            };

            var expectedDto = new ClientSiteDto
            {
                Id = 1,
                SiteCode = "SITE-BLR-001",
                Name = "HDFC Bank Regional Office",
                Latitude = 12.9715987m,
                Longitude = 77.5945627m,
                RadiusMeters = 150,
                Address = "42 MG Road, Bengaluru, Karnataka",
                IsActive = true
            };

            _mediatorMock.Setup(m => m.Send(It.IsAny<CreateClientSiteCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedDto);

            // Act
            var result = await _controller.RegisterClientSite(request, CancellationToken.None);

            // Assert
            var statusResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status201Created, statusResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<ClientSiteDto>>(statusResult.Value);
            Assert.True(apiResponse.Success);
            Assert.Equal("SITE-BLR-001", apiResponse.Data!.SiteCode);
            Assert.True(apiResponse.Data.IsActive);
        }

        // --- Negative Cases ---

        [Fact]
        public async Task RegisterClientSite_NullRequest_Returns400BadRequest()
        {
            // Act
            var result = await _controller.RegisterClientSite(null!, CancellationToken.None);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
            Assert.False(apiResponse.Success);
            Assert.Equal("BAD_REQUEST", apiResponse.Error!.Code);
        }

        [Fact]
        public async Task RegisterClientSite_DuplicateSiteCode_Returns409Conflict()
        {
            // Arrange
            var request = new CreateClientSiteRequest
            {
                SiteCode = "SITE-EXISTING",
                Name = "Duplicate Site Attempt",
                Latitude = 13.0m,
                Longitude = 77.0m,
                Address = "123 Business Boulevard, Bangalore"
            };

            _mediatorMock.Setup(m => m.Send(It.IsAny<CreateClientSiteCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Site with code 'SITE-EXISTING' already exists."));

            // Act
            var result = await _controller.RegisterClientSite(request, CancellationToken.None);

            // Assert
            var conflictResult = Assert.IsType<ConflictObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(conflictResult.Value);
            Assert.Equal("DUPLICATE_SITE_CODE", apiResponse.Error!.Code);
        }

        [Fact]
        public async Task RegisterClientSite_ValidationArgumentException_Returns400BadRequest()
        {
            // Arrange
            var request = new CreateClientSiteRequest
            {
                SiteCode = "SITE-INVALID",
                Name = "Invalid Coordinates Site",
                Latitude = 95.0m, // Invalid latitude (> 90)
                Longitude = 77.0m,
                Address = "Valid address length test"
            };

            _mediatorMock.Setup(m => m.Send(It.IsAny<CreateClientSiteCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Latitude must be between -90.0 and +90.0."));

            // Act
            var result = await _controller.RegisterClientSite(request, CancellationToken.None);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
            Assert.Equal("VALIDATION_ERROR", apiResponse.Error!.Code);
        }

        [Fact]
        public async Task RegisterClientSite_UnauthorizedCaller_Returns403Forbidden()
        {
            // Arrange
            var request = new CreateClientSiteRequest
            {
                SiteCode = "SITE-FORBIDDEN",
                Name = "Forbidden Site",
                Latitude = 12.0m,
                Longitude = 77.0m,
                Address = "123 Restricted Facility Avenue"
            };

            _mediatorMock.Setup(m => m.Send(It.IsAny<CreateClientSiteCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new UnauthorizedAccessException("Employees are not authorized to register client sites."));

            // Act
            var result = await _controller.RegisterClientSite(request, CancellationToken.None);

            // Assert
            var statusResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status403Forbidden, statusResult.StatusCode);
            var apiResponse = Assert.IsType<ApiResponse<object>>(statusResult.Value);
            Assert.Equal("FORBIDDEN", apiResponse.Error!.Code);
        }

        // --- Edge Cases ---

        [Theory]
        [InlineData(90.0, 180.0)]     // Extreme North-East corner
        [InlineData(-90.0, -180.0)]   // Extreme South-West corner
        [InlineData(0.0000001, 0.0)]  // Valid non-zero near equator
        public async Task RegisterClientSite_CoordinateBoundaries_Succeeds(decimal lat, decimal lon)
        {
            // Arrange
            var request = new CreateClientSiteRequest
            {
                SiteCode = "SITE-BOUNDARY",
                Name = "Boundary Geographic Site",
                Latitude = lat,
                Longitude = lon,
                RadiusMeters = 100,
                Address = "Extreme Coordinate Geographic Point"
            };

            var dto = new ClientSiteDto
            {
                Id = 99,
                SiteCode = "SITE-BOUNDARY",
                Latitude = lat,
                Longitude = lon
            };

            _mediatorMock.Setup(m => m.Send(It.IsAny<CreateClientSiteCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(dto);

            // Act
            var result = await _controller.RegisterClientSite(request, CancellationToken.None);

            // Assert
            var statusResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status201Created, statusResult.StatusCode);
        }

        [Theory]
        [InlineData(20)]    // Min boundary radius
        [InlineData(1000)]  // Max boundary radius
        public async Task RegisterClientSite_RadiusMetersBoundaries_Succeeds(int radius)
        {
            // Arrange
            var request = new CreateClientSiteRequest
            {
                SiteCode = "SITE-RADIUS",
                Name = "Radius Boundary Site",
                Latitude = 12.9m,
                Longitude = 77.5m,
                RadiusMeters = radius,
                Address = "Valid address length boundary test"
            };

            var dto = new ClientSiteDto { Id = 1, RadiusMeters = radius };
            _mediatorMock.Setup(m => m.Send(It.IsAny<CreateClientSiteCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(dto);

            // Act
            var result = await _controller.RegisterClientSite(request, CancellationToken.None);

            // Assert
            var statusResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status201Created, statusResult.StatusCode);
        }

        #endregion

        #region 2. GetAllClientSites API Tests (GET /api/v1/ClientSites)

        // --- Positive Cases ---

        [Fact]
        public async Task GetAllClientSites_ActiveOnlyTrue_Returns200OKWithActiveSites()
        {
            // Arrange
            var site1 = CreateDomainSite(1, "SITE-BLR-001", active: true);
            var site2 = CreateDomainSite(2, "SITE-BLR-002", active: true);
            _repoMock.Setup(r => r.GetAllAsync(true))
                .ReturnsAsync(new List<ClientSite> { site1, site2 });

            // Act
            var result = await _controller.GetAllClientSites(activeOnly: true, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<List<ClientSiteDto>>>(okResult.Value);
            Assert.True(apiResponse.Success);
            Assert.Equal(2, apiResponse.Data!.Count);
        }

        [Fact]
        public async Task GetAllClientSites_ActiveOnlyFalse_ReturnsAllSites()
        {
            // Arrange
            var site1 = CreateDomainSite(1, "SITE-BLR-001", active: true);
            var site2 = CreateDomainSite(2, "SITE-DEACT-002", active: false);
            _repoMock.Setup(r => r.GetAllAsync(false))
                .ReturnsAsync(new List<ClientSite> { site1, site2 });

            // Act
            var result = await _controller.GetAllClientSites(activeOnly: false, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<List<ClientSiteDto>>>(okResult.Value);
            Assert.Equal(2, apiResponse.Data!.Count);
        }

        // --- Edge Cases ---

        [Fact]
        public async Task GetAllClientSites_EmptyDatabase_Returns200OKWithEmptyList()
        {
            // Arrange
            _repoMock.Setup(r => r.GetAllAsync(It.IsAny<bool>()))
                .ReturnsAsync(new List<ClientSite>());

            // Act
            var result = await _controller.GetAllClientSites(activeOnly: true, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<List<ClientSiteDto>>>(okResult.Value);
            Assert.True(apiResponse.Success);
            Assert.Empty(apiResponse.Data!);
        }

        #endregion

        #region 3. GetClientSiteById API Tests (GET /api/v1/ClientSites/{id})

        // --- Positive Cases ---

        [Fact]
        public async Task GetClientSiteById_ExistingId_Returns200OKWithSiteDetails()
        {
            // Arrange
            var site = CreateDomainSite(1, "SITE-BLR-001");
            _repoMock.Setup(r => r.GetByIdAsync(1))
                .ReturnsAsync(site);

            // Act
            var result = await _controller.GetClientSiteById(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<ClientSiteDto>>(okResult.Value);
            Assert.True(apiResponse.Success);
            Assert.NotNull(apiResponse.Data);
            Assert.Equal("SITE-BLR-001", apiResponse.Data.SiteCode);
            Assert.Equal(site.Name, apiResponse.Data.Name);
        }

        // --- Negative Cases ---

        [Fact]
        public async Task GetClientSiteById_NonExistentId_Returns404NotFound()
        {
            // Arrange
            _repoMock.Setup(r => r.GetByIdAsync(99999))
                .ReturnsAsync((ClientSite?)null);

            // Act
            var result = await _controller.GetClientSiteById(99999);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(notFoundResult.Value);
            Assert.False(apiResponse.Success);
            Assert.Equal("NOT_FOUND", apiResponse.Error!.Code);
        }

        [Fact]
        public async Task GetClientSiteById_NegativeId_Returns404NotFound()
        {
            // Arrange
            _repoMock.Setup(r => r.GetByIdAsync(-10))
                .ReturnsAsync((ClientSite?)null);

            // Act
            var result = await _controller.GetClientSiteById(-10);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(notFoundResult.Value);
            Assert.False(apiResponse.Success);
            Assert.Equal("NOT_FOUND", apiResponse.Error!.Code);
        }

        // --- Edge Cases ---

        [Fact]
        public async Task GetClientSiteById_MaxIntId_Returns404NotFoundWithoutCrash()
        {
            // Arrange
            _repoMock.Setup(r => r.GetByIdAsync(int.MaxValue))
                .ReturnsAsync((ClientSite?)null);

            // Act
            var result = await _controller.GetClientSiteById(int.MaxValue);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal(StatusCodes.Status404NotFound, notFoundResult.StatusCode);
        }

        #endregion
    }
}
