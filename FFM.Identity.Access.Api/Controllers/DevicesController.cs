using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using FFM.Identity.Access.Api.Models;
using FFM.Identity.Access.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FFM.Identity.Access.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DevicesController : ControllerBase
    {
        private readonly IDeviceService _deviceService;

        public DevicesController(IDeviceService deviceService)
        {
            _deviceService = deviceService ?? throw new ArgumentNullException(nameof(deviceService));
        }

        /// <summary>
        /// Retrieves all registered devices for the currently authenticated employee.
        /// </summary>
        [HttpGet("my")]
        public async Task<ActionResult<List<DeviceDto>>> GetMyDevices()
        {
            var userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized();

            var devices = await _deviceService.GetUserDevicesAsync(userId);
            return Ok(devices);
        }

        /// <summary>
        /// Registers a mobile device for the authenticated employee.
        /// </summary>
        [HttpPost("register")]
        public async Task<IActionResult> RegisterDevice([FromBody] RegisterDeviceRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized();

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (success, device, message) = await _deviceService.RegisterDeviceAsync(userId, request, ipAddress);

            if (!success || device == null)
            {
                return BadRequest(new { success = false, message });
            }

            return Ok(new { success = true, device, message });
        }

        /// <summary>
        /// Revokes access from a registered device (e.g. if lost, replaced, or decommissioned).
        /// </summary>
        [HttpPost("revoke")]
        public async Task<IActionResult> RevokeDevice([FromBody] RevokeDeviceRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized();

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (success, message) = await _deviceService.RevokeDeviceAsync(userId, request.DeviceId, request.Reason, ipAddress);

            if (!success)
            {
                return BadRequest(new { success = false, message });
            }

            return Ok(new { success = true, message });
        }

        /// <summary>
        /// Approves and activates a pending device registration (Manager / Admin privilege).
        /// </summary>
        [HttpPost("approve")]
        [Authorize(Roles = "SYSTEM_ADMIN,ADMIN,MANAGER")]
        public async Task<IActionResult> ApproveDevice([FromBody] ApproveDeviceRequest request)
        {
            var userId = GetCurrentUserId();
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (success, message) = await _deviceService.ApproveDeviceAsync(request.DeviceId, userId, ipAddress);

            if (!success)
            {
                return BadRequest(new { success = false, message });
            }

            return Ok(new { success = true, message });
        }

        /// <summary>
        /// Lists all device bindings across the organization (Manager / Admin privilege).
        /// </summary>
        [HttpGet("all")]
        [Authorize(Roles = "SYSTEM_ADMIN,ADMIN,MANAGER")]
        public async Task<ActionResult<List<DeviceDto>>> GetAllDevices()
        {
            var devices = await _deviceService.GetAllDevicesAsync();
            return Ok(devices);
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("userId")?.Value
                     ?? User.FindFirst("sub")?.Value;

            if (int.TryParse(claim, out var id)) return id;
            return 0;
        }
    }
}
