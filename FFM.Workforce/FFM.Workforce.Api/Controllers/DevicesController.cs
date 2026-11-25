using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using FFM.Identity.Access.Domain.Entities;
using FFM.Workforce.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FFM.Workforce.Api.Controllers
{
    [ApiController]
    [Route("api/devices")]
    [Route("api/v1/devices")]
    [Authorize]
    public class DevicesController : ControllerBase
    {
        private readonly WorkforceDbContext _dbContext;

        public DevicesController(WorkforceDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public record RegisterDeviceRequest(string DeviceUuid, string? DeviceModel = null, string? OsVersion = null);
        public record RevokeDeviceRequest(Guid DeviceId, string? Reason = null);
        public record ApproveDeviceRequest(Guid DeviceId);

        public record DeviceDto(
            Guid Id,
            int UserId,
            string DeviceUuid,
            string Status,
            DateTimeOffset CreatedAt,
            DateTimeOffset? ApprovedAt);

        /// <summary>
        /// Retrieves all registered devices for the currently authenticated employee.
        /// </summary>
        [HttpGet("my")]
        public async Task<IActionResult> GetMyDevices()
        {
            var userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized();

            var devices = await _dbContext.UserDevices
                .Where(d => d.UserId == userId)
                .OrderByDescending(d => d.CreatedAt)
                .Select(d => new DeviceDto(
                    d.Id,
                    d.UserId,
                    d.DeviceUuid,
                    d.Status.ToString(),
                    d.CreatedAt,
                    d.ApprovedAt))
                .ToListAsync();

            return Ok(devices);
        }

        /// <summary>
        /// Registers a mobile device for the authenticated employee.
        /// </summary>
        [HttpPost("register")]
        public async Task<IActionResult> RegisterDevice([FromBody] RegisterDeviceRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.DeviceUuid))
            {
                return BadRequest(new { success = false, message = "Device UUID is required." });
            }

            var userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized();

            var cleanUuid = request.DeviceUuid.Trim();

            // Check if device is already registered
            var existing = await _dbContext.UserDevices.FirstOrDefaultAsync(d => d.DeviceUuid == cleanUuid);
            if (existing != null)
            {
                if (existing.UserId == userId)
                {
                    return Ok(new
                    {
                        success = true,
                        device = new DeviceDto(existing.Id, existing.UserId, existing.DeviceUuid, existing.Status.ToString(), existing.CreatedAt, existing.ApprovedAt),
                        message = "Device already registered for this user."
                    });
                }

                return BadRequest(new { success = false, message = "This device is already bound to another employee account." });
            }

            // If user has no active devices, set as Active; otherwise Pending approval
            var hasActiveDevice = await _dbContext.UserDevices.AnyAsync(d => d.UserId == userId && d.Status == DeviceStatus.Active);
            var initialStatus = hasActiveDevice ? DeviceStatus.Pending : DeviceStatus.Active;

            var device = new UserDevice
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                DeviceUuid = cleanUuid,
                Status = initialStatus,
                CreatedAt = DateTimeOffset.UtcNow,
                ApprovedAt = initialStatus == DeviceStatus.Active ? DateTimeOffset.UtcNow : null
            };

            _dbContext.UserDevices.Add(device);
            await _dbContext.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                device = new DeviceDto(device.Id, device.UserId, device.DeviceUuid, device.Status.ToString(), device.CreatedAt, device.ApprovedAt),
                message = initialStatus == DeviceStatus.Active
                    ? "Device successfully registered and activated."
                    : "Device registered and pending administrator approval."
            });
        }

        /// <summary>
        /// Revokes access from a registered device (e.g. if lost, replaced, or decommissioned).
        /// </summary>
        [HttpPost("revoke")]
        public async Task<IActionResult> RevokeDevice([FromBody] RevokeDeviceRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized();

            var device = await _dbContext.UserDevices.FirstOrDefaultAsync(d => d.Id == request.DeviceId && d.UserId == userId);
            if (device == null)
            {
                return NotFound(new { success = false, message = "Device not found or not owned by this account." });
            }

            device.Status = DeviceStatus.Revoked;
            await _dbContext.SaveChangesAsync();

            return Ok(new { success = true, message = "Device access has been revoked successfully." });
        }

        /// <summary>
        /// Approves and activates a pending device registration (Manager / Admin privilege).
        /// </summary>
        [HttpPost("approve")]
        [Authorize(Roles = "SYSTEM_ADMIN,ADMIN,MANAGER")]
        public async Task<IActionResult> ApproveDevice([FromBody] ApproveDeviceRequest request)
        {
            var device = await _dbContext.UserDevices.FindAsync(request.DeviceId);
            if (device == null)
            {
                return NotFound(new { success = false, message = "Device not found." });
            }

            // Revoke any previous active devices for this user (single active device policy)
            var otherActive = await _dbContext.UserDevices
                .Where(d => d.UserId == device.UserId && d.Id != device.Id && d.Status == DeviceStatus.Active)
                .ToListAsync();

            foreach (var d in otherActive)
            {
                d.Status = DeviceStatus.Replaced;
            }

            device.Status = DeviceStatus.Active;
            device.ApprovedAt = DateTimeOffset.UtcNow;

            await _dbContext.SaveChangesAsync();

            return Ok(new { success = true, message = "Device approved and activated. Previous devices were replaced." });
        }

        /// <summary>
        /// Lists all device bindings across the organization (Manager / Admin privilege).
        /// </summary>
        [HttpGet("all")]
        [Authorize(Roles = "SYSTEM_ADMIN,ADMIN,MANAGER")]
        public async Task<IActionResult> GetAllDevices()
        {
            var devices = await _dbContext.UserDevices
                .OrderByDescending(d => d.CreatedAt)
                .Select(d => new DeviceDto(
                    d.Id,
                    d.UserId,
                    d.DeviceUuid,
                    d.Status.ToString(),
                    d.CreatedAt,
                    d.ApprovedAt))
                .ToListAsync();

            return Ok(devices);
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("userId")?.Value
                     ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            if (int.TryParse(claim, out var id)) return id;
            return 0;
        }
    }
}
