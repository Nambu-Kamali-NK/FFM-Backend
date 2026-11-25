using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using FFM.Identity.Access.Domain.Entities;
using FFM.Workforce.Api.Controllers;
using FFM.Workforce.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FFM.Workforce.Tests
{
    public class DeviceBindingTests
    {
        private static JsonElement ToJson(object? obj)
        {
            var json = JsonSerializer.Serialize(obj);
            return JsonDocument.Parse(json).RootElement;
        }

        private WorkforceDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<WorkforceDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new WorkforceDbContext(options);
        }

        [Fact]
        public async Task AuthController_Login_MobileClient_FirstDevice_AutoBindsAndSucceeds()
        {
            var db = GetInMemoryDbContext();
            var password = "SecurePassword123!";
            var user = new User
            {
                UsernameOrEmail = "field.tech1@ffm.om",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Status = AccountStatus.Active,
                Role = Role.EMPLOYEE,
                MfaEnabled = false
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var controller = new AuthController(db);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
            controller.Request.Headers["X-Client-Id"] = "ffm-mobile";

            // Login with initial device UUID
            var loginResult = await controller.Login(new AuthController.LoginRequest(
                "field.tech1@ffm.om",
                password,
                DeviceId: "MCT-PHONE-DEV-001"
            ));

            var okResult = Assert.IsType<OkObjectResult>(loginResult);
            var data = ToJson(okResult.Value);

            Assert.True(data.GetProperty("success").GetBoolean());
            Assert.NotNull(data.GetProperty("accessToken").GetString());

            // Verify device was recorded as Active in database
            var deviceInDb = await db.UserDevices.FirstOrDefaultAsync(d => d.UserId == user.Id);
            Assert.NotNull(deviceInDb);
            Assert.Equal("MCT-PHONE-DEV-001", deviceInDb.DeviceUuid);
            Assert.Equal(DeviceStatus.Active, deviceInDb.Status);
        }

        [Fact]
        public async Task AuthController_Login_MobileClient_MatchingDevice_Succeeds()
        {
            var db = GetInMemoryDbContext();
            var password = "SecurePassword123!";
            var user = new User
            {
                UsernameOrEmail = "field.tech2@ffm.om",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Status = AccountStatus.Active,
                Role = Role.EMPLOYEE,
                MfaEnabled = false
            };
            db.Users.Add(user);

            // Pre-seed active device
            var boundDevice = new UserDevice
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                DeviceUuid = "MCT-PHONE-DEV-002",
                Status = DeviceStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                ApprovedAt = DateTimeOffset.UtcNow
            };
            db.UserDevices.Add(boundDevice);
            await db.SaveChangesAsync();

            var controller = new AuthController(db);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
            controller.Request.Headers["X-Client-Id"] = "ffm-mobile";

            var loginResult = await controller.Login(new AuthController.LoginRequest(
                "field.tech2@ffm.om",
                password,
                DeviceId: "MCT-PHONE-DEV-002"
            ));

            var okResult = Assert.IsType<OkObjectResult>(loginResult);
            var data = ToJson(okResult.Value);

            Assert.True(data.GetProperty("success").GetBoolean());
            Assert.NotNull(data.GetProperty("accessToken").GetString());
        }

        [Fact]
        public async Task AuthController_Login_MobileClient_MismatchedDevice_Returns403Forbidden()
        {
            var db = GetInMemoryDbContext();
            var password = "SecurePassword123!";
            var user = new User
            {
                UsernameOrEmail = "field.tech3@ffm.om",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Status = AccountStatus.Active,
                Role = Role.EMPLOYEE,
                MfaEnabled = false
            };
            db.Users.Add(user);

            // Pre-seed active device
            var boundDevice = new UserDevice
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                DeviceUuid = "MCT-AUTHORIZED-PHONE-777",
                Status = DeviceStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                ApprovedAt = DateTimeOffset.UtcNow
            };
            db.UserDevices.Add(boundDevice);
            await db.SaveChangesAsync();

            var controller = new AuthController(db);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
            controller.Request.Headers["X-Client-Id"] = "ffm-mobile";

            // Attempt login from an unauthorized foreign device
            var loginResult = await controller.Login(new AuthController.LoginRequest(
                "field.tech3@ffm.om",
                password,
                DeviceId: "MCT-UNAUTHORIZED-PHONE-999"
            ));

            var objResult = Assert.IsType<ObjectResult>(loginResult);
            Assert.Equal(403, objResult.StatusCode);

            var data = ToJson(objResult.Value);
            Assert.False(data.GetProperty("success").GetBoolean());
            Assert.True(data.GetProperty("deviceBindingError").GetBoolean());
            Assert.Contains("Device mismatch", data.GetProperty("message").GetString());
        }

        [Fact]
        public async Task DevicesController_RegisterAndApproveWorkflow_ReplacesPreviousActiveDevice()
        {
            var db = GetInMemoryDbContext();
            var user = new User
            {
                UsernameOrEmail = "technician.fleet@ffm.om",
                PasswordHash = "hash",
                Status = AccountStatus.Active,
                Role = Role.EMPLOYEE
            };
            db.Users.Add(user);

            // Active device 1
            var oldDevice = new UserDevice
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                DeviceUuid = "PHONE-OLD-001",
                Status = DeviceStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow.AddMonths(-3),
                ApprovedAt = DateTimeOffset.UtcNow.AddMonths(-3)
            };
            db.UserDevices.Add(oldDevice);
            await db.SaveChangesAsync();

            var controller = new DevicesController(db);

            // Authenticated context
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new("userId", user.Id.ToString()),
                new(ClaimTypes.Role, "ADMIN")
            };
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
                }
            };

            // 1. Register new replacement device -> should be set to Pending because user already has an active device
            var registerResult = await controller.RegisterDevice(new DevicesController.RegisterDeviceRequest("PHONE-NEW-002"));
            var okReg = Assert.IsType<OkObjectResult>(registerResult);
            var regData = ToJson(okReg.Value);

            Assert.True(regData.GetProperty("success").GetBoolean());
            var newDeviceId = Guid.Parse(regData.GetProperty("device").GetProperty("Id").GetString()!);
            Assert.Equal("Pending", regData.GetProperty("device").GetProperty("Status").GetString());

            // 2. Manager approves replacement device
            var approveResult = await controller.ApproveDevice(new DevicesController.ApproveDeviceRequest(newDeviceId));
            var okApprove = Assert.IsType<OkObjectResult>(approveResult);
            var approveData = ToJson(okApprove.Value);
            Assert.True(approveData.GetProperty("success").GetBoolean());

            // 3. Verify in database: new device is Active, old device is Replaced
            var updatedNew = await db.UserDevices.FindAsync(newDeviceId);
            Assert.Equal(DeviceStatus.Active, updatedNew!.Status);
            Assert.NotNull(updatedNew.ApprovedAt);

            var updatedOld = await db.UserDevices.FindAsync(oldDevice.Id);
            Assert.Equal(DeviceStatus.Replaced, updatedOld!.Status);
        }

        [Fact]
        public async Task DevicesController_RevokeDevice_SetsStatusToRevoked()
        {
            var db = GetInMemoryDbContext();
            var user = new User
            {
                UsernameOrEmail = "technician.lost@ffm.om",
                PasswordHash = "hash",
                Status = AccountStatus.Active,
                Role = Role.EMPLOYEE
            };
            db.Users.Add(user);

            var device = new UserDevice
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                DeviceUuid = "PHONE-LOST-999",
                Status = DeviceStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                ApprovedAt = DateTimeOffset.UtcNow
            };
            db.UserDevices.Add(device);
            await db.SaveChangesAsync();

            var controller = new DevicesController(db);
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new("userId", user.Id.ToString())
            };
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
                }
            };

            var revokeResult = await controller.RevokeDevice(new DevicesController.RevokeDeviceRequest(device.Id, "Device lost"));
            var okRevoke = Assert.IsType<OkObjectResult>(revokeResult);
            var revokeData = ToJson(okRevoke.Value);

            Assert.True(revokeData.GetProperty("success").GetBoolean());

            var updatedDevice = await db.UserDevices.FindAsync(device.Id);
            Assert.Equal(DeviceStatus.Revoked, updatedDevice!.Status);
        }
    }
}
