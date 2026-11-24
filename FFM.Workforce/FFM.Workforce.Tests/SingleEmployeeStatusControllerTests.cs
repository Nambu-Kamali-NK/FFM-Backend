using System.Threading;
using System.Threading.Tasks;
using FFM.Workforce.Api.Controllers;
using FFM.Workforce.Application.Commands;
using FFM.Workforce.Application.DTOs;
using FFM.Workforce.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FFM.Workforce.Tests
{
    public class SingleEmployeeStatusControllerTests
    {
        private readonly Mock<IMediator> _mediatorMock = new();
        private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
        private readonly EmployeesController _controller;

        public SingleEmployeeStatusControllerTests()
        {
            _currentUserServiceMock.Setup(c => c.GetUserId()).Returns(101);
            _controller = new EmployeesController(_mediatorMock.Object, _currentUserServiceMock.Object);
        }

        [Fact]
        public async Task UpdateSingleEmployeeStatus_WithIsActiveTrue_SendsActivateEmployeeCommand()
        {
            // Arrange
            var request = new SingleEmployeeStatusChangeRequest { Id = 42, IsActive = true };

            // Act
            var result = await _controller.UpdateSingleEmployeeStatus(request, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

            _mediatorMock.Verify(m => m.Send(
                It.Is<ActivateEmployeeCommand>(c => c.EmployeeId == 42 && c.ActorUserId == 101),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateSingleEmployeeStatus_WithIsActiveFalse_SendsDeactivateEmployeeCommand()
        {
            // Arrange
            var request = new SingleEmployeeStatusChangeRequest { Id = 42, IsActive = false };

            // Act
            var result = await _controller.UpdateSingleEmployeeStatus(request, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

            _mediatorMock.Verify(m => m.Send(
                It.Is<DeactivateEmployeeCommand>(c => c.EmployeeId == 42 && c.ActorUserId == 101),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateSingleEmployeeStatus_WithZeroOrNegativeId_ReturnsBadRequest()
        {
            // Arrange
            var requestZero = new SingleEmployeeStatusChangeRequest { Id = 0, IsActive = true };
            var requestNegative = new SingleEmployeeStatusChangeRequest { Id = -5, IsActive = true };

            // Act
            var resultZero = await _controller.UpdateSingleEmployeeStatus(requestZero, CancellationToken.None);
            var resultNegative = await _controller.UpdateSingleEmployeeStatus(requestNegative, CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(resultZero);
            Assert.IsType<BadRequestObjectResult>(resultNegative);
            _mediatorMock.Verify(m => m.Send(It.IsAny<IRequest<bool>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UpdateSingleEmployeeStatus_WithNullBody_ReturnsBadRequest()
        {
            // Act
            var result = await _controller.UpdateSingleEmployeeStatus(null!, CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
            _mediatorMock.Verify(m => m.Send(It.IsAny<IRequest<bool>>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
