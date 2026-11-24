using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFM.Workforce.Application.DTOs;
using FFM.Workforce.Application.Handlers;
using FFM.Workforce.Application.Interfaces;
using FFM.Workforce.Application.Queries;
using FFM.Workforce.Domain.Entities;
using Moq;
using Xunit;

namespace FFM.Workforce.Tests
{
    public class GetAllEmployeesQueryHandlerTests
    {
        private readonly Mock<IEmployeeRepository> _employeeRepoMock;
        private readonly Mock<ICurrentUserService> _currentUserMock;
        private readonly GetAllEmployeesQueryHandler _handler;

        public GetAllEmployeesQueryHandlerTests()
        {
            _employeeRepoMock = new Mock<IEmployeeRepository>();
            _currentUserMock = new Mock<ICurrentUserService>();
            _handler = new GetAllEmployeesQueryHandler(_employeeRepoMock.Object, _currentUserMock.Object);
        }

        [Fact]
        public async Task Handle_AdminRole_ReturnsAllEmployeesPaged()
        {
            // Arrange
            var empList = new List<Employee>
            {
                new Employee(1, "EMP1", 10, "Admin User", 1, Guid.NewGuid(), null, "admin@test.com", "9876543210", "System Admin"),
                new Employee(2, "EMP2", 11, "Manager User", 2, Guid.NewGuid(), null, "mgr@test.com", "9876543211", "System Admin"),
                new Employee(3, "EMP3", 12, "Staff User", 6, Guid.NewGuid(), 2, "staff@test.com", "9876543212", "System Admin")
            };

            _employeeRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(empList);
            _currentUserMock.Setup(u => u.GetRoles()).Returns(new[] { "ADMIN" });
            _currentUserMock.Setup(u => u.GetUserId()).Returns(10);

            // Act
            var result = await _handler.Handle(new GetAllEmployeesQuery(), CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.TotalCount);
            Assert.Equal(3, result.Items.Count);
        }

        [Fact]
        public async Task Handle_SortOrderAscendingAndDescending_SortsByName()
        {
            // Arrange
            var emp1 = new Employee(1, "EMP1", 10, "Alice", 1, Guid.NewGuid(), null, "a@test.com", "9876543210", "System Admin");
            var emp2 = new Employee(2, "EMP2", 11, "Zack", 2, Guid.NewGuid(), null, "z@test.com", "9876543211", "System Admin");

            _employeeRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Employee> { emp1, emp2 });
            _currentUserMock.Setup(u => u.GetRoles()).Returns(new[] { "ADMIN" });

            var queryDesc = new GetAllEmployeesQuery
            {
                SortBy = "Name",
                SortOrder = "desc"
            };

            var queryAsc = new GetAllEmployeesQuery
            {
                SortBy = "Name",
                SortOrder = "asc"
            };

            // Act
            var resultDesc = await _handler.Handle(queryDesc, CancellationToken.None);
            var resultAsc = await _handler.Handle(queryAsc, CancellationToken.None);

            // Assert
            Assert.Equal("Zack", resultDesc.Items[0].Name);
            Assert.Equal("Alice", resultDesc.Items[1].Name);

            Assert.Equal("Alice", resultAsc.Items[0].Name);
            Assert.Equal("Zack", resultAsc.Items[1].Name);
        }

        [Fact]
        public async Task Handle_Pagination_AppliesPageSizeAndPageNumber()
        {
            // Arrange
            var empList = new List<Employee>();
            for (int i = 1; i <= 25; i++)
            {
                empList.Add(new Employee(i, $"EMP{i}", i, $"User {i}", 6, Guid.NewGuid(), null, $"u{i}@test.com", "1234567890", "System Admin"));
            }

            _employeeRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(empList);
            _currentUserMock.Setup(u => u.GetRoles()).Returns(new[] { "ADMIN" });

            var query = new GetAllEmployeesQuery
            {
                PageNumber = 2,
                PageSize = 10
            };

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(25, result.TotalCount);
            Assert.Equal(3, result.TotalPages);
            Assert.Equal(10, result.Items.Count);
            Assert.True(result.HasPreviousPage);
            Assert.True(result.HasNextPage);
        }
    }
}
