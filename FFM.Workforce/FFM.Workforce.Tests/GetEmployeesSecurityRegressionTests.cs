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
    public class GetEmployeesSecurityRegressionTests
    {
        private readonly Mock<IEmployeeRepository> _employeeRepoMock;
        private readonly Mock<ICurrentUserService> _currentUserMock;

        public GetEmployeesSecurityRegressionTests()
        {
            _employeeRepoMock = new Mock<IEmployeeRepository>();
            _currentUserMock = new Mock<ICurrentUserService>();
        }

        [Fact]
        public async Task Bola_Employee_CannotAccessOtherEmployeeById()
        {
            var handler = new GetEmployeeByIdQueryHandler(_employeeRepoMock.Object, _currentUserMock.Object);

            var otherEmp = new Employee(10, "EMP10", 99, "Other User", 6, Guid.NewGuid(), null, "other@test.com", "9876543210", "System Admin");
            _employeeRepoMock.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(otherEmp);

            _currentUserMock.Setup(u => u.GetUserId()).Returns(50); // Calling user is 50, target employee is 99
            _currentUserMock.Setup(u => u.GetRoles()).Returns(new[] { "EMPLOYEE" });

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                handler.Handle(new GetEmployeeByIdQuery(10), CancellationToken.None));
        }

        [Fact]
        public async Task Bola_Manager_CannotAccessEmployeeInDifferentOrgUnit()
        {
            var handler = new GetEmployeeByIdQueryHandler(_employeeRepoMock.Object, _currentUserMock.Object);

            var managerEmp = new Employee(2, "MGR1", 20, "Manager", 4, Guid.NewGuid(), null, "mgr@test.com", "9876543211", "System Admin"); // OrgUnit 1
            var outsideEmp = new Employee(10, "EMP10", 99, "Outside User", 6, Guid.NewGuid(), null, "outside@test.com", "9876543210", "System Admin"); // OrgUnit 2

            _employeeRepoMock.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(outsideEmp);
            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(20)).ReturnsAsync(managerEmp);

            _currentUserMock.Setup(u => u.GetUserId()).Returns(20);
            _currentUserMock.Setup(u => u.GetRoles()).Returns(new[] { "MANAGER" });

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                handler.Handle(new GetEmployeeByIdQuery(10), CancellationToken.None));
        }

        [Fact]
        public async Task GetAll_EmployeeRole_EnforcesStrictSelfBoundary()
        {
            var handler = new GetAllEmployeesQueryHandler(_employeeRepoMock.Object, _currentUserMock.Object);

            var callerEmp = new Employee(5, "EMP5", 50, "Caller", 6, Guid.NewGuid(), null, "caller@test.com", "9876543210", "System Admin");
            var empList = new List<Employee>
            {
                new Employee(1, "EMP1", 10, "Admin", 1, Guid.NewGuid(), null, "admin@test.com", "9876543211", "System Admin"),
                callerEmp,
                new Employee(9, "EMP9", 90, "Peer", 6, Guid.NewGuid(), null, "peer@test.com", "9876543212", "System Admin")
            };

            _employeeRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(empList);
            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(50)).ReturnsAsync(callerEmp);

            _currentUserMock.Setup(u => u.GetUserId()).Returns(50);
            _currentUserMock.Setup(u => u.GetRoles()).Returns(new[] { "EMPLOYEE" });

            var result = await handler.Handle(new GetAllEmployeesQuery(), CancellationToken.None);

            Assert.NotNull(result);
            Assert.Single(result.Items);
            Assert.Equal(5, result.Items[0].Id);
        }

        [Fact]
        public async Task GetAll_ManagerRole_FiltersOutsideOrgUnitEmployees()
        {
            var handler = new GetAllEmployeesQueryHandler(_employeeRepoMock.Object, _currentUserMock.Object);

            var managerEmp = new Employee(2, "MGR1", 20, "Manager", 4, Guid.NewGuid(), null, "mgr@test.com", "9876543211", "System Admin");
            var empList = new List<Employee>
            {
                new Employee(1, "EMP1", 10, "Outside Employee", 6, Guid.NewGuid(), null, "out@test.com", "9876543210", "System Admin"), // OrgUnit 2
                managerEmp, // OrgUnit 1
                new Employee(3, "EMP3", 30, "Inside Employee", 6, Guid.NewGuid(), 2, "in@test.com", "9876543212", "System Admin") // OrgUnit 1
            };

            _employeeRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(empList);
            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(20)).ReturnsAsync(managerEmp);

            _currentUserMock.Setup(u => u.GetUserId()).Returns(20);
            _currentUserMock.Setup(u => u.GetRoles()).Returns(new[] { "MANAGER" });

            var result = await handler.Handle(new GetAllEmployeesQuery(), CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(2, result.Items.Count);
            Assert.DoesNotContain(result.Items, e => e.Id == 1);
        }
    }
}
