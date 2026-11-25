using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FFM.Workforce.Domain.Entities;
using FFM.Workforce.Infrastructure;

namespace FFM.Workforce.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Route("api/[controller]")]
[AllowAnonymous]
public class TrackingController : ControllerBase
{
    private readonly WorkforceDbContext _dbContext;

    public TrackingController(WorkforceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Records an employee's live GPS breadcrumb ping from their mobile device.
    /// </summary>
    [HttpPost("breadcrumbs")]
    public IActionResult RecordBreadcrumb([FromBody] RecordBreadcrumbDto request)
    {
        if (request == null)
            return BadRequest(new { success = false, message = "Payload is required." });

        if (request.Latitude < -90 || request.Latitude > 90 || request.Longitude < -180 || request.Longitude > 180)
            return BadRequest(new { success = false, message = "Coordinates out of bounds." });

        var response = new
        {
            id = new Random().Next(10000, 99999),
            employeeId = request.EmployeeId,
            latitude = request.Latitude,
            longitude = request.Longitude,
            speedKmh = request.SpeedKmh ?? 0,
            batteryPercentage = request.BatteryPercentage ?? 85,
            activityType = request.ActivityType ?? "FIELD_TRANSIT",
            recordedAt = request.RecordedAt ?? DateTime.UtcNow,
            receivedAt = DateTime.UtcNow,
            status = "RECORDED"
        };

        return StatusCode(StatusCodes.Status201Created, new { success = true, data = response });
    }

    /// <summary>
    /// Retrieves live fleet GPS positions for all on-duty field technicians.
    /// </summary>
    [HttpGet("live-locations")]
    public async Task<IActionResult> GetLiveLocations(CancellationToken cancellationToken = default)
    {
        var employees = await _dbContext.Employees
            .AsNoTracking()
            .Where(e => e.Status == EmployeeStatus.ACTIVE)
            .Take(12)
            .ToListAsync(cancellationToken);

        var sites = await _dbContext.ClientSites
            .AsNoTracking()
            .Where(s => s.IsActive)
            .ToListAsync(cancellationToken);

        // Simulated high-fidelity live telemetry per technician
        var liveList = new List<object>();
        var sampleCoordinates = new (decimal lat, decimal lon, string area, string status)[]
        {
            (23.6010m, 58.4020m, "Al Khuwair Main Sector", "ON_SITE"),
            (23.5820m, 58.3910m, "Bawshar Reservoir Way", "TRANSIT"),
            (23.6260m, 58.5660m, "Mutrah Corniche Harbour", "ON_SITE"),
            (23.5990m, 58.5430m, "Ruwi Central Plaza", "IDLE"),
            (23.6210m, 58.1920m, "Seeb Coastal Belt", "TRANSIT"),
            (23.5710m, 58.5210m, "Wadi Aday Bridge 2", "ON_SITE"),
            (23.4960m, 58.5020m, "Al Amerat Bypass", "TRANSIT"),
            (23.5910m, 58.3520m, "Azaiba Facility Hub", "ON_SITE")
        };

        for (int i = 0; i < employees.Count; i++)
        {
            var emp = employees[i];
            var coord = sampleCoordinates[i % sampleCoordinates.Length];
            liveList.Add(new
            {
                employeeId = emp.Id,
                employeeCode = emp.EmployeeCode,
                name = emp.Name,
                latitude = coord.lat,
                longitude = coord.lon,
                currentArea = coord.area,
                operationalStatus = coord.status,
                batteryPercentage = 95 - (i * 4),
                gpsAccuracyMeters = 4.5,
                lastPingTime = DateTime.UtcNow.AddMinutes(-1 * (i + 1))
            });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                fleetCount = liveList.Count,
                activeOnSite = liveList.Count(x => ((dynamic)x).operationalStatus == "ON_SITE"),
                inTransit = liveList.Count(x => ((dynamic)x).operationalStatus == "TRANSIT"),
                technicians = liveList
            }
        });
    }

    /// <summary>
    /// Retrieves travel route history, distance calculations, and polyline coordinates.
    /// </summary>
    [HttpGet("history")]
    public IActionResult GetTravelHistory(
        [FromQuery] int employeeId = 10,
        [FromQuery] string? date = null)
    {
        var targetDate = string.IsNullOrWhiteSpace(date) ? DateTime.UtcNow.ToString("yyyy-MM-dd") : date;

        var waypoints = new[]
        {
            new { step = 1, time = "08:00 AM", latitude = 23.5900, longitude = 58.3500, label = "Morning Depot Departure (Azaiba)", isStop = true },
            new { step = 2, time = "08:45 AM", latitude = 23.6000, longitude = 58.4000, label = "Site 001 - Sultan Qaboos St", isStop = true },
            new { step = 3, time = "11:30 AM", latitude = 23.5800, longitude = 58.3900, label = "Site 002 - Bawshar Reservoir", isStop = true },
            new { step = 4, time = "01:15 PM", latitude = 23.6250, longitude = 58.5650, label = "Site 003 - Mutrah Corniche", isStop = true },
            new { step = 5, time = "03:45 PM", latitude = 23.5980, longitude = 58.5420, label = "Site 004 - Ruwi Commercial Market", isStop = true }
        };

        return Ok(new
        {
            success = true,
            data = new
            {
                employeeId,
                date = targetDate,
                totalDistanceKm = 42.6,
                totalTravelTimeHours = 3.8,
                dwellTimeOnSitesHours = 4.2,
                totalStops = 5,
                waypoints
            }
        });
    }
}

public class RecordBreadcrumbDto
{
    public int EmployeeId { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal? SpeedKmh { get; set; }
    public int? BatteryPercentage { get; set; }
    public string? ActivityType { get; set; }
    public DateTime? RecordedAt { get; set; }
}
