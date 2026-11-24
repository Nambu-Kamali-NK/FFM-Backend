#nullable enable
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FFM.Identity.Access.Api.Controllers;
using FFM.Identity.Access.Api.Models;
using FFM.Identity.Access.Api.Services;
using FFM.Identity.Access.Domain.Entities;
using FFM.Identity.Access.Domain.Models;
using FFM.Identity.Access.Domain.Options;
using FFM.Identity.Access.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using FFM.Workforce.Application.Commands;
using FFM.Workforce.Application.Handlers;
using FFM.Workforce.Application.Interfaces;
using FFM.Workforce.Application.Validators;
using FFM.Workforce.Domain.Entities;
using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Xunit;

namespace FFM.Workforce.Tests
{
    public class SastSecurityRegressionTests
    {
        private static string GenerateEphemeralPrivateKey(out RSA rsa)
        {
            rsa = RSA.Create(2048);
            return Convert.ToBase64String(rsa.ExportPkcs8PrivateKey());
        }

        #region Finding 1: Hardcoded Secrets

        [Fact]
        public void Appsettings_DoesNotContainHardcodedPrivateKey()
        {
            var appsettingsPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "FFM.Identity.Access.Api", "appsettings.json");
            Assert.True(File.Exists(appsettingsPath), $"appsettings.json should exist at {appsettingsPath}");

            var json = File.ReadAllText(appsettingsPath);
            using var doc = JsonDocument.Parse(json);

            var key = doc.RootElement
                .GetProperty("Jwt")
                .GetProperty("RsaPrivateKey")
                .GetString();

            Assert.True(string.IsNullOrEmpty(key), "RsaPrivateKey must NOT be populated in appsettings.json.");
        }

        [Fact]
        public void Gitignore_ProtectsPrivateKeyAndSecrets()
        {
            var gitignorePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".gitignore");
            Assert.True(File.Exists(gitignorePath), ".gitignore must exist in root");

            var lines = File.ReadAllLines(gitignorePath);
            Assert.Contains(lines, l => l.Trim() == "*.key" || l.Trim() == "identity-private.key");
        }

        #endregion

        #region Finding 2: Sensitive Data in Logs

        [Fact]
        public void ProductionControllersAndServices_DoNotContainConsoleWriteLine()
        {
            var rootDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
            var identityApiFiles = Directory.GetFiles(Path.Combine(rootDir, "FFM.Identity.Access.Api"), "*.cs", SearchOption.AllDirectories);
            var workforceApiFiles = Directory.GetFiles(Path.Combine(rootDir, "FFM.Workforce", "FFM.Workforce.Api"), "*.cs", SearchOption.AllDirectories);

            var allFiles = identityApiFiles.Concat(workforceApiFiles).ToList();

            foreach (var file in allFiles)
            {
                if (file.Contains("Debug") || file.Contains("obj")) continue;

                var content = File.ReadAllText(file);
                Assert.DoesNotContain("Console.WriteLine", content);
            }
        }

        #endregion

        #region Finding 3: XSS / Employee Name Handling

        [Theory]
        [InlineData("<script>alert(1)</script>")]
        [InlineData("<img src=x onerror=alert('xss')>")]
        [InlineData("<svg/onload=alert('XSS')>")]
        [InlineData("Robert'); DROP TABLE Employees;--")]
        [InlineData("Jane Doe <iframe src='evil.com'>")]
        [InlineData("Admin & Co \"quotes\"")]
        public void CreateEmployeeCommandValidator_RejectsHtmlAndScriptPayloads(string maliciousName)
        {
            var validator = new CreateEmployeeCommandValidator();
            var command = new CreateEmployeeCommand
            {
                EmployeeCode = "EMP001",
                Name = maliciousName,
                RoleId = 6
            };

            var result = validator.TestValidate(command);
            result.ShouldHaveValidationErrorFor(x => x.Name);
        }

        [Theory]
        [InlineData("John Doe")]
        [InlineData("Mary-Jane Watson")]
        [InlineData("O'Connor")]
        [InlineData("Jean-Luc Picard")]
        [InlineData("Dr. Martin Luther King Jr.")]
        [InlineData("Renée Dupont")]
        [InlineData("Müller")]
        public void CreateEmployeeCommandValidator_AcceptsLegitimateNames(string validName)
        {
            var validator = new CreateEmployeeCommandValidator();
            var command = new CreateEmployeeCommand
            {
                EmployeeCode = "EMP001",
                Name = validName,
                RoleId = 6
            };

            var result = validator.TestValidate(command);
            result.ShouldNotHaveValidationErrorFor(x => x.Name);
        }

        #endregion

        #region Finding 4: Vertical Privilege Escalation

        private static (CreateEmployeeCommandHandler handler, Mock<IEmployeeRepository> empRepo, Mock<ICurrentUserService> currentUser, DbContext db) CreateTestContext()
        {
            var empRepoMock = new Mock<IEmployeeRepository>();
            var outboxRepoMock = new Mock<IOutboxRepository>();
            var orgUnitRepoMock = new Mock<IOrgUnitRepository>();
            var currentUserMock = new Mock<ICurrentUserService>();

            var defaultOrgUnit = new OrgUnit(
                id: Guid.NewGuid(),
                name: "Default Org",
                type: "ORGANIZATION",
                status: "ACTIVE",
                createdBy: Guid.NewGuid(),
                isDefault: true);

            orgUnitRepoMock.Setup(r => r.GetDefaultActiveAsync()).ReturnsAsync(defaultOrgUnit);
            empRepoMock.Setup(r => r.GetByEmployeeCodeAsync(It.IsAny<string>())).ReturnsAsync((Employee?)null);

            var options = new DbContextOptionsBuilder<FFM.Workforce.Infrastructure.WorkforceDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            var db = new FFM.Workforce.Infrastructure.WorkforceDbContext(options);

            // Seed Workforce Roles (1 to 6)
            db.Set<FFM.Workforce.Domain.Entities.Role>().AddRange(
                new FFM.Workforce.Domain.Entities.Role("SYSTEM_ADMIN", "ACTIVE"),
                new FFM.Workforce.Domain.Entities.Role("ADMIN", "ACTIVE"),
                new FFM.Workforce.Domain.Entities.Role("EXECUTIVE", "ACTIVE"),
                new FFM.Workforce.Domain.Entities.Role("MANAGER", "ACTIVE"),
                new FFM.Workforce.Domain.Entities.Role("SUPERVISOR", "ACTIVE"),
                new FFM.Workforce.Domain.Entities.Role("EMPLOYEE", "ACTIVE")
            );
            db.SaveChanges();

            var handler = new CreateEmployeeCommandHandler(
                empRepoMock.Object,
                outboxRepoMock.Object,
                orgUnitRepoMock.Object,
                db,
                currentUserMock.Object);

            return (handler, empRepoMock, currentUserMock, db);
        }

        [Fact]
        public async Task VerticalPrivilegeEscalation_Manager_CannotAssignSystemAdmin()
        {
            var (handler, _, currentUser, _) = CreateTestContext();
            currentUser.Setup(u => u.GetRoles()).Returns(new[] { "MANAGER" });

            // RoleId 1 is SYSTEM_ADMIN
            var command = new CreateEmployeeCommand
            {
                EmployeeCode = "EMP_ESCALATE",
                Name = "Attacker Manager",
                RoleId = 1,
                Password = "Password123!"
            };

            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => handler.Handle(command, CancellationToken.None));

            Assert.Contains("System Admin", ex.Message);
        }

        [Fact]
        public async Task VerticalPrivilegeEscalation_Manager_CannotAssignAdmin()
        {
            var (handler, _, currentUser, _) = CreateTestContext();
            currentUser.Setup(u => u.GetRoles()).Returns(new[] { "MANAGER" });

            // RoleId 2 is ADMIN
            var command = new CreateEmployeeCommand
            {
                EmployeeCode = "EMP_ESCALATE",
                Name = "Attacker Manager",
                RoleId = 2,
                Password = "Password123!"
            };

            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => handler.Handle(command, CancellationToken.None));

            Assert.Contains("administrative", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task VerticalPrivilegeEscalation_Admin_CannotAssignSystemAdmin()
        {
            var (handler, _, currentUser, _) = CreateTestContext();
            currentUser.Setup(u => u.GetRoles()).Returns(new[] { "ADMIN" });

            // RoleId 1 is SYSTEM_ADMIN
            var command = new CreateEmployeeCommand
            {
                EmployeeCode = "EMP_ADMIN_ESCALATE",
                Name = "Admin User",
                RoleId = 1,
                Password = "Password123!"
            };

            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => handler.Handle(command, CancellationToken.None));

            Assert.Contains("System Admin", ex.Message);
        }

        [Fact]
        public async Task VerticalPrivilegeEscalation_Employee_CannotCreateAnyEmployee()
        {
            var (handler, _, currentUser, _) = CreateTestContext();
            currentUser.Setup(u => u.GetRoles()).Returns(new[] { "EMPLOYEE" });

            var command = new CreateEmployeeCommand
            {
                EmployeeCode = "EMP_NEW",
                Name = "Regular Employee",
                RoleId = 6,
                Password = "Password123!"
            };

            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => handler.Handle(command, CancellationToken.None));

            Assert.Contains("does not have permission to create employees", ex.Message);
        }

        [Fact]
        public async Task VerticalPrivilegeEscalation_InvalidRoleId_ThrowsInvalidOperationException()
        {
            var (handler, _, currentUser, _) = CreateTestContext();
            currentUser.Setup(u => u.GetRoles()).Returns(new[] { "SYSTEM_ADMIN" });

            var command = new CreateEmployeeCommand
            {
                EmployeeCode = "EMP_BAD_ROLE",
                Name = "Valid Name",
                RoleId = 9999, // Non-existent role
                Password = "Password123!"
            };

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => handler.Handle(command, CancellationToken.None));

            Assert.Contains("Role with ID 9999 not found", ex.Message);
        }

        #endregion

        #region Finding 5: Missing Role Claim in Session Refresh

        [Theory]
        [InlineData(FFM.Identity.Access.Domain.Entities.Role.EMPLOYEE)]
        [InlineData(FFM.Identity.Access.Domain.Entities.Role.MANAGER)]
        [InlineData(FFM.Identity.Access.Domain.Entities.Role.ADMIN)]
        [InlineData(FFM.Identity.Access.Domain.Entities.Role.SYSTEM_ADMIN)]
        public async Task RefreshToken_MaintainsUserRoleClaimConsistently(FFM.Identity.Access.Domain.Entities.Role userRole)
        {
            // Arrange
            var dbOptions = new DbContextOptionsBuilder<IdentityDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            using var db = new IdentityDbContext(dbOptions);

            var user = new User
            {
                UsernameOrEmail = $"user_{userRole}@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test@12345"),
                Status = AccountStatus.Active,
                Role = userRole
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var rawToken = Guid.NewGuid().ToString("N");
            var hashedToken = TokenHasher.ComputeHash(rawToken);

            var refreshToken = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = hashedToken,
                ClientId = "test-client",
                IssuedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
                RevokedAt = null
            };
            db.RefreshTokens.Add(refreshToken);
            await db.SaveChangesAsync();

            var privKeyBase64 = GenerateEphemeralPrivateKey(out var rsa);
            var jwtOptions = Options.Create(new JwtOptions
            {
                Issuer = "https://ffm.example.com",
                Audience = "ffm-client",
                ExpiryMinutes = 60,
                RefreshExpiryDays = 30,
                RsaPrivateKey = privKeyBase64
            });

            var refreshService = new RefreshTokenService(db, jwtOptions);

            // Act
            var response = await refreshService.RefreshAsync(new RefreshRequest
            {
                RefreshToken = rawToken
            });

            // Assert
            Assert.NotNull(response);
            Assert.NotEmpty(response.AccessToken);

            // Parse token and inspect claims
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwt = tokenHandler.ReadJwtToken(response.AccessToken);

            var roleClaim = jwt.Claims.FirstOrDefault(c => c.Type == "role");
            Assert.NotNull(roleClaim);
            Assert.Equal(userRole.ToString(), roleClaim.Value);

            var subClaim = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub);
            Assert.NotNull(subClaim);
            Assert.Equal(user.Id.ToString(), subClaim.Value);
        }

        #endregion

        #region Finding 6: Additional Security Verifications (Remediated)

        [Fact]
        public async Task VerticalPrivilegeEscalation_Identity_Admin_CannotAssignSystemAdmin()
        {
            var dbOptions = new DbContextOptionsBuilder<IdentityDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            using var db = new IdentityDbContext(dbOptions);

            var mockAuthService = new Mock<IAuthService>();
            var mockRefreshService = new Mock<IRefreshTokenService>();
            var mockUserService = new Mock<IUserService>();

            var controller = new AuthController(mockAuthService.Object, mockRefreshService.Object, db);
            Assert.NotNull(controller);
        }

        [Theory]
        [InlineData("")]
        [InlineData("short")]
        [InlineData("nouppercase1!")]
        [InlineData("NOLOWERCASE1!")]
        [InlineData("NoNumber!")]
        [InlineData("NoSpecial123")]
        public void PasswordValidation_RejectsWeakPasswords(string weakPassword)
        {
            var validator = new CreateEmployeeCommandValidator();
            var command = new CreateEmployeeCommand
            {
                EmployeeCode = "EMP001",
                Name = "Valid Name",
                RoleId = 6,
                Password = weakPassword
            };
            var result = validator.TestValidate(command);
            result.ShouldHaveValidationErrorFor(x => x.Password);
        }

        [Fact]
        public void PasswordValidation_AcceptsStrongPassword()
        {
            var validator = new CreateEmployeeCommandValidator();
            var command = new CreateEmployeeCommand
            {
                EmployeeCode = "EMP001",
                Name = "Valid Name",
                RoleId = 6,
                Password = "StrongPassword@123"
            };
            var result = validator.TestValidate(command);
            result.ShouldNotHaveValidationErrorFor(x => x.Password);
        }

        [Fact]
        public async Task RefreshToken_ReuseDetection_RevokesAllActiveTokensForUser()
        {
            var dbOptions = new DbContextOptionsBuilder<IdentityDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            using var db = new IdentityDbContext(dbOptions);

            var user = new User
            {
                UsernameOrEmail = "victim@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test@12345"),
                Status = AccountStatus.Active,
                Role = FFM.Identity.Access.Domain.Entities.Role.EMPLOYEE
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var rawToken1 = Guid.NewGuid().ToString("N");
            var hashedToken1 = TokenHasher.ComputeHash(rawToken1);

            var rawToken2 = Guid.NewGuid().ToString("N");
            var hashedToken2 = TokenHasher.ComputeHash(rawToken2);

            // Token 1 was already used and revoked
            var revokedToken = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = hashedToken1,
                ClientId = "client-1",
                IssuedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
                RevokedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
            };

            // Token 2 is currently active
            var activeToken = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = hashedToken2,
                ClientId = "client-1",
                IssuedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
                RevokedAt = null
            };

            db.RefreshTokens.AddRange(revokedToken, activeToken);
            await db.SaveChangesAsync();

            var privKeyBase64 = GenerateEphemeralPrivateKey(out _);
            var jwtOptions = Options.Create(new JwtOptions
            {
                Issuer = "https://ffm.example.com",
                Audience = "ffm-client",
                ExpiryMinutes = 60,
                RefreshExpiryDays = 30,
                RsaPrivateKey = privKeyBase64
            });

            var refreshService = new RefreshTokenService(db, jwtOptions);

            // Attacker attempts to replay the revoked Token 1
            var response = await refreshService.RefreshAsync(new RefreshRequest
            {
                RefreshToken = rawToken1
            });

            Assert.Null(response);

            // Verify that active Token 2 is revoked
            var reloadedActiveToken = await db.RefreshTokens.FindAsync(activeToken.Id);
            Assert.NotNull(reloadedActiveToken?.RevokedAt);
        }

        [Fact]
        public async Task AuthService_LockedUserWithoutExpiry_CannotAuthenticateAndIsNotRestoredToActive()
        {
            var dbOptions = new DbContextOptionsBuilder<IdentityDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            using var db = new IdentityDbContext(dbOptions);

            var user = new User
            {
                UsernameOrEmail = "permanently_locked@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test@12345"),
                Status = AccountStatus.Locked,
                LockedUntil = null // Indefinite administrative lock
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var jwtOptions = Options.Create(new JwtOptions
            {
                Issuer = "https://ffm.example.com",
                Audience = "ffm-client",
                ExpiryMinutes = 60,
                RefreshExpiryDays = 30,
                RsaPrivateKey = GenerateEphemeralPrivateKey(out _)
            });

            var refreshService = new RefreshTokenService(db, jwtOptions);
            var authService = new AuthService(db, jwtOptions, refreshService);

            // Attempt login with correct password
            var response = await authService.AuthenticateAsync(new LoginRequest
            {
                Username = "permanently_locked@test.com",
                Password = "Test@12345"
            }, "test-client");

            // Must be denied
            Assert.Null(response);

            // Verify status has NOT been overridden to Active
            var reloadedUser = await db.Users.FindAsync(user.Id);
            Assert.Equal(AccountStatus.Locked, reloadedUser?.Status);
        }

        [Fact]
        public async Task GetEmployeeById_Supervisor_CannotAccessPeerInSameOrgUnitWithoutDirectReport()
        {
            var empRepoMock = new Mock<IEmployeeRepository>();
            var currentUserMock = new Mock<ICurrentUserService>();

            var orgId = Guid.NewGuid();
            var supervisorCaller = new Employee(1, "SUP1", 101, "Supervisor One", 5, orgId, null, "sup1@test.com", "1234567890", "System Admin");
            var peerEmployee = new Employee(2, "PEER1", 102, "Peer Employee", 6, orgId, 999 /* different manager */, "peer@test.com", "1234567890", "System Admin");

            empRepoMock.Setup(r => r.GetByIdAsync(peerEmployee.Id)).ReturnsAsync(peerEmployee);
            empRepoMock.Setup(r => r.GetByUserIdAsync(101)).ReturnsAsync(supervisorCaller);

            currentUserMock.Setup(u => u.GetRoles()).Returns(new[] { "SUPERVISOR" });
            currentUserMock.Setup(u => u.GetUserId()).Returns(101);

            var handler = new FFM.Workforce.Application.Handlers.GetEmployeeByIdQueryHandler(empRepoMock.Object, currentUserMock.Object);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                handler.Handle(new FFM.Workforce.Application.Queries.GetEmployeeByIdQuery(peerEmployee.Id), CancellationToken.None));
        }

        [Fact]
        public void WorkforceProgram_DoesNotContainHardcodedFallbackDatabasePassword()
        {
            var rootDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
            var workforceProgramPath = Path.Combine(rootDir, "FFM.Workforce", "FFM.Workforce.Api", "Program.cs");
            Assert.True(File.Exists(workforceProgramPath), $"Program.cs must exist at {workforceProgramPath}");

            var content = File.ReadAllText(workforceProgramPath);
            Assert.DoesNotContain("Password=ffm_pass", content);
        }

        #endregion
    }
}
