#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FFM.Identity.Access.Api.Services;
using FFM.Identity.Access.Domain.Entities;
using FFM.Identity.Access.Domain.Models;
using FFM.Identity.Access.Domain.Options;
using FFM.Identity.Access.Infrastructure.Data;
using FFM.Workforce.Application.Commands;
using FFM.Workforce.Application.DTOs;
using FFM.Workforce.Application.Handlers;
using FFM.Workforce.Application.Interfaces;
using FFM.Workforce.Application.Queries;
using FFM.Workforce.Application.Validators;
using FFM.Workforce.Domain.Entities;
using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FFM.Workforce.Tests
{
    public class SecurityRegressionTests
    {
        private static readonly Guid OrgUnitA = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid OrgUnitB = Guid.Parse("22222222-2222-2222-2222-222222222222");

        #region 1. BOLA / IDOR — Workforce Employee API Tests

        [Theory]
        [InlineData("SYSTEM_ADMIN")]
        [InlineData("ADMIN")]
        [InlineData("EXECUTIVE")]
        public async Task GetEmployeeById_AdministrativeRoles_CanAccessAnyEmployee(string role)
        {
            // Arrange
            var empRepoMock = new Mock<IEmployeeRepository>();
            var currentUserMock = new Mock<ICurrentUserService>();

            var targetEmp = new Employee(100, "EMP100", 100, "Target Emp", 6, OrgUnitB, null, "target@test.com", "1234567890", "System Admin");
            empRepoMock.Setup(r => r.GetByIdAsync(targetEmp.Id)).ReturnsAsync(targetEmp);

            currentUserMock.Setup(u => u.GetRoles()).Returns(new[] { role });
            currentUserMock.Setup(u => u.GetUserId()).Returns(999); // completely unrelated user ID

            var handler = new GetEmployeeByIdQueryHandler(empRepoMock.Object, currentUserMock.Object);

            // Act
            var result = await handler.Handle(new GetEmployeeByIdQuery(targetEmp.Id), CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(targetEmp.Id, result.Id);
            Assert.Equal("Target Emp", result.Name);
        }

        [Fact]
        public async Task GetEmployeeById_Employee_CanAccessOwnRecord()
        {
            // Arrange
            var empRepoMock = new Mock<IEmployeeRepository>();
            var currentUserMock = new Mock<ICurrentUserService>();

            var ownEmp = new Employee(10, "EMP10", 10, "Own Emp", 6, OrgUnitA, null, "own@test.com", "1234567890", "System Admin");
            empRepoMock.Setup(r => r.GetByIdAsync(ownEmp.Id)).ReturnsAsync(ownEmp);

            currentUserMock.Setup(u => u.GetRoles()).Returns(new[] { "EMPLOYEE" });
            currentUserMock.Setup(u => u.GetUserId()).Returns(10); // matches ownEmp.UserId

            var handler = new GetEmployeeByIdQueryHandler(empRepoMock.Object, currentUserMock.Object);

            // Act
            var result = await handler.Handle(new GetEmployeeByIdQuery(ownEmp.Id), CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(ownEmp.Id, result.Id);
        }

        [Fact]
        public async Task GetEmployeeById_Employee_CannotAccessOtherEmployee_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            var empRepoMock = new Mock<IEmployeeRepository>();
            var currentUserMock = new Mock<ICurrentUserService>();

            var otherEmp = new Employee(20, "EMP20", 20, "Other Emp", 6, OrgUnitA, null, "other@test.com", "1234567890", "System Admin");
            empRepoMock.Setup(r => r.GetByIdAsync(otherEmp.Id)).ReturnsAsync(otherEmp);

            currentUserMock.Setup(u => u.GetRoles()).Returns(new[] { "EMPLOYEE" });
            currentUserMock.Setup(u => u.GetUserId()).Returns(10); // different user ID

            var handler = new GetEmployeeByIdQueryHandler(empRepoMock.Object, currentUserMock.Object);

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                handler.Handle(new GetEmployeeByIdQuery(otherEmp.Id), CancellationToken.None));
        }

        [Fact]
        public async Task GetEmployeeById_Manager_CanAccessDirectReport()
        {
            // Arrange
            var empRepoMock = new Mock<IEmployeeRepository>();
            var currentUserMock = new Mock<ICurrentUserService>();

            var managerEmp = new Employee(50, "MGR1", 50, "Manager 1", 4, OrgUnitA, null, "mgr@test.com", "1234567890", "System Admin");
            var reportEmp = new Employee(51, "REP1", 51, "Report 1", 6, OrgUnitB, managerEmp.Id, "rep@test.com", "1234567890", "System Admin");

            empRepoMock.Setup(r => r.GetByIdAsync(reportEmp.Id)).ReturnsAsync(reportEmp);
            empRepoMock.Setup(r => r.GetByUserIdAsync(50)).ReturnsAsync(managerEmp);

            currentUserMock.Setup(u => u.GetRoles()).Returns(new[] { "MANAGER" });
            currentUserMock.Setup(u => u.GetUserId()).Returns(50);

            var handler = new GetEmployeeByIdQueryHandler(empRepoMock.Object, currentUserMock.Object);

            // Act
            var result = await handler.Handle(new GetEmployeeByIdQuery(reportEmp.Id), CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(reportEmp.Id, result.Id);
        }

        [Fact]
        public async Task GetEmployeeById_Manager_CanAccessEmployeeInSameOrgUnit()
        {
            // Arrange
            var empRepoMock = new Mock<IEmployeeRepository>();
            var currentUserMock = new Mock<ICurrentUserService>();

            var managerEmp = new Employee(50, "MGR1", 50, "Manager 1", 4, OrgUnitA, null, "mgr@test.com", "1234567890", "System Admin");
            var peerEmp = new Employee(52, "PEER1", 52, "Peer Emp", 6, OrgUnitA, null, "peer@test.com", "1234567890", "System Admin");

            empRepoMock.Setup(r => r.GetByIdAsync(peerEmp.Id)).ReturnsAsync(peerEmp);
            empRepoMock.Setup(r => r.GetByUserIdAsync(50)).ReturnsAsync(managerEmp);

            currentUserMock.Setup(u => u.GetRoles()).Returns(new[] { "MANAGER" });
            currentUserMock.Setup(u => u.GetUserId()).Returns(50);

            var handler = new GetEmployeeByIdQueryHandler(empRepoMock.Object, currentUserMock.Object);

            // Act
            var result = await handler.Handle(new GetEmployeeByIdQuery(peerEmp.Id), CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(peerEmp.Id, result.Id);
        }

        [Fact]
        public async Task GetEmployeeById_Manager_CannotAccessEmployeeInDifferentOrgUnitAndNotDirectReport()
        {
            // Arrange
            var empRepoMock = new Mock<IEmployeeRepository>();
            var currentUserMock = new Mock<ICurrentUserService>();

            var managerEmp = new Employee(50, "MGR1", 50, "Manager 1", 4, OrgUnitA, null, "mgr@test.com", "1234567890", "System Admin");
            var outsideEmp = new Employee(53, "OUT1", 53, "Outside Emp", 6, OrgUnitB, 999, "out@test.com", "1234567890", "System Admin");

            empRepoMock.Setup(r => r.GetByIdAsync(outsideEmp.Id)).ReturnsAsync(outsideEmp);
            empRepoMock.Setup(r => r.GetByUserIdAsync(50)).ReturnsAsync(managerEmp);

            currentUserMock.Setup(u => u.GetRoles()).Returns(new[] { "MANAGER" });
            currentUserMock.Setup(u => u.GetUserId()).Returns(50);

            var handler = new GetEmployeeByIdQueryHandler(empRepoMock.Object, currentUserMock.Object);

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                handler.Handle(new GetEmployeeByIdQuery(outsideEmp.Id), CancellationToken.None));
        }

        [Fact]
        public async Task GetEmployeeById_NonExistentEmployee_ReturnsNull()
        {
            // Arrange
            var empRepoMock = new Mock<IEmployeeRepository>();
            var currentUserMock = new Mock<ICurrentUserService>();

            empRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Employee?)null);

            var handler = new GetEmployeeByIdQueryHandler(empRepoMock.Object, currentUserMock.Object);

            // Act
            var result = await handler.Handle(new GetEmployeeByIdQuery(999), CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region 2. Input Validation Pipeline Tests

        private readonly CreateEmployeeCommandValidator _validator = new();

        [Fact]
        public void Validate_ValidCommand_Passes()
        {
            var command = new CreateEmployeeCommand
            {
                EmployeeCode = "EMP001",
                Name = "John Doe",
                RoleId = 6,
                Password = "Password@123",
                Email = "john.doe@example.com",
                Mobile = "9876543210"
            };

            var result = _validator.TestValidate(command);
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData(null)]
        public void Validate_MissingEmployeeCode_Fails(string? code)
        {
            var command = new CreateEmployeeCommand
            {
                EmployeeCode = code!,
                Name = "John Doe",
                RoleId = 6
            };

            var result = _validator.TestValidate(command);
            result.ShouldHaveValidationErrorFor(x => x.EmployeeCode);
        }

        [Fact]
        public void Validate_EmployeeCodeTooLong_Fails()
        {
            var command = new CreateEmployeeCommand
            {
                EmployeeCode = new string('A', 25), // max 20
                Name = "John Doe",
                RoleId = 6
            };

            var result = _validator.TestValidate(command);
            result.ShouldHaveValidationErrorFor(x => x.EmployeeCode);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData(null)]
        public void Validate_MissingName_Fails(string? name)
        {
            var command = new CreateEmployeeCommand
            {
                EmployeeCode = "EMP001",
                Name = name!,
                RoleId = 6
            };

            var result = _validator.TestValidate(command);
            result.ShouldHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void Validate_NameTooLong_Fails()
        {
            var command = new CreateEmployeeCommand
            {
                EmployeeCode = "EMP001",
                Name = new string('X', 105), // max 100
                RoleId = 6
            };

            var result = _validator.TestValidate(command);
            result.ShouldHaveValidationErrorFor(x => x.Name);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-99)]
        public void Validate_InvalidRoleId_Fails(int roleId)
        {
            var command = new CreateEmployeeCommand
            {
                EmployeeCode = "EMP001",
                Name = "John Doe",
                RoleId = roleId
            };

            var result = _validator.TestValidate(command);
            result.ShouldHaveValidationErrorFor(x => x.RoleId);
        }

        [Fact]
        public void Validate_InvalidEmailFormat_Fails()
        {
            var command = new CreateEmployeeCommand
            {
                EmployeeCode = "EMP001",
                Name = "John Doe",
                RoleId = 6,
                Email = "not-a-valid-email"
            };

            var result = _validator.TestValidate(command);
            result.ShouldHaveValidationErrorFor(x => x.Email);
        }

        [Theory]
        [InlineData("123")] // too short
        [InlineData("abcdef12345")] // non-digits
        [InlineData("12345678901234567")] // too long (>15 digits)
        public void Validate_InvalidMobileFormat_Fails(string mobile)
        {
            var command = new CreateEmployeeCommand
            {
                EmployeeCode = "EMP001",
                Name = "John Doe",
                RoleId = 6,
                Mobile = mobile
            };

            var result = _validator.TestValidate(command);
            result.ShouldHaveValidationErrorFor(x => x.Mobile);
        }

        #endregion

        #region 3. Authentication Status Enforcement Tests

        [Fact]
        public async Task Authenticate_ActiveAccount_Succeeds()
        {
            // Arrange
            var dbOptions = new DbContextOptionsBuilder<IdentityDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            using var db = new IdentityDbContext(dbOptions);

            var password = "SecurePassword123!";
            var user = new User
            {
                UsernameOrEmail = "active@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Status = AccountStatus.Active,
                Role = FFM.Identity.Access.Domain.Entities.Role.EMPLOYEE
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var jwtOptions = Options.Create(new JwtOptions
            {
                Issuer = "https://ffm.example.com",
                Audience = "ffm-client",
                ExpiryMinutes = 60,
                RefreshExpiryDays = 30,
                RsaPrivateKey = GenerateEphemeralPrivateKey()
            });

            var refreshServiceMock = new Mock<IRefreshTokenService>();
            var authService = new AuthService(db, jwtOptions, refreshServiceMock.Object);

            // Act
            var response = await authService.AuthenticateAsync(new LoginRequest
            {
                Username = "active@test.com",
                Password = password
            }, "client1");

            // Assert
            Assert.NotNull(response);
            Assert.NotEmpty(response.AccessToken);
        }

        [Theory]
        [InlineData(AccountStatus.PendingActivation)]
        [InlineData(AccountStatus.Suspended)]
        [InlineData(AccountStatus.Blocked)]
        [InlineData(AccountStatus.Deactivated)]
        public async Task Authenticate_InactiveStatuses_ReturnsNull(AccountStatus status)
        {
            // Arrange
            var dbOptions = new DbContextOptionsBuilder<IdentityDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            using var db = new IdentityDbContext(dbOptions);

            var password = "SecurePassword123!";
            var user = new User
            {
                UsernameOrEmail = $"user_{status}@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Status = status,
                Role = FFM.Identity.Access.Domain.Entities.Role.EMPLOYEE
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var jwtOptions = Options.Create(new JwtOptions
            {
                Issuer = "https://ffm.example.com",
                Audience = "ffm-client",
                ExpiryMinutes = 60,
                RsaPrivateKey = GenerateEphemeralPrivateKey()
            });

            var refreshServiceMock = new Mock<IRefreshTokenService>();
            var authService = new AuthService(db, jwtOptions, refreshServiceMock.Object);

            // Act
            var response = await authService.AuthenticateAsync(new LoginRequest
            {
                Username = $"user_{status}@test.com",
                Password = password
            }, "client1");

            // Assert: Must be rejected with null
            Assert.Null(response);
        }

        [Fact]
        public async Task Authenticate_LockedAccount_Unexpired_ReturnsNull()
        {
            // Arrange
            var dbOptions = new DbContextOptionsBuilder<IdentityDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            using var db = new IdentityDbContext(dbOptions);

            var password = "SecurePassword123!";
            var user = new User
            {
                UsernameOrEmail = "locked@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Status = AccountStatus.Locked,
                LockedUntil = DateTimeOffset.UtcNow.AddMinutes(10), // currently locked
                Role = FFM.Identity.Access.Domain.Entities.Role.EMPLOYEE
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var jwtOptions = Options.Create(new JwtOptions
            {
                Issuer = "https://ffm.example.com",
                Audience = "ffm-client",
                ExpiryMinutes = 60,
                RsaPrivateKey = GenerateEphemeralPrivateKey()
            });

            var refreshServiceMock = new Mock<IRefreshTokenService>();
            var authService = new AuthService(db, jwtOptions, refreshServiceMock.Object);

            // Act
            var response = await authService.AuthenticateAsync(new LoginRequest
            {
                Username = "locked@test.com",
                Password = password
            }, "client1");

            // Assert: Must be rejected with null
            Assert.Null(response);
        }

        [Fact]
        public async Task Authenticate_InvalidPassword_ReturnsNull()
        {
            // Arrange
            var dbOptions = new DbContextOptionsBuilder<IdentityDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            using var db = new IdentityDbContext(dbOptions);

            var user = new User
            {
                UsernameOrEmail = "wrongpass@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPassword123!"),
                Status = AccountStatus.Active,
                Role = FFM.Identity.Access.Domain.Entities.Role.EMPLOYEE
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var jwtOptions = Options.Create(new JwtOptions
            {
                Issuer = "https://ffm.example.com",
                Audience = "ffm-client",
                ExpiryMinutes = 60,
                RsaPrivateKey = GenerateEphemeralPrivateKey()
            });

            var refreshServiceMock = new Mock<IRefreshTokenService>();
            var authService = new AuthService(db, jwtOptions, refreshServiceMock.Object);

            // Act
            var response = await authService.AuthenticateAsync(new LoginRequest
            {
                Username = "wrongpass@test.com",
                Password = "WrongPassword999!"
            }, "client1");

            // Assert: Must be rejected with null
            Assert.Null(response);
        }

        private static string GenerateEphemeralPrivateKey()
        {
            using var rsa = System.Security.Cryptography.RSA.Create(2048);
            return Convert.ToBase64String(rsa.ExportPkcs8PrivateKey());
        }

        #endregion
    }
}
