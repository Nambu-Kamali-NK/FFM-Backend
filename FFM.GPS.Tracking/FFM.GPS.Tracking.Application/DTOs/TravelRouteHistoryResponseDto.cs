using System;
using System.Collections.Generic;

namespace FFM.GPS.Tracking.Application.DTOs;

public class TravelRouteHistoryResponseDto
{
    public int UserId { get; set; }
    public string Date { get; set; } = string.Empty;
    public RouteMetricsDto Metrics { get; set; } = new();
    public List<RouteStopDto> Stops { get; set; } = new();
    public List<RoutePointDto> Polyline { get; set; } = new();
}

public class RouteMetricsDto
{
    public double TotalDistanceKilometers { get; set; }
    public int TotalBreadcrumbsCount { get; set; }
    public DateTime? ShiftStartTime { get; set; }
    public DateTime? ShiftEndTime { get; set; }
    public int TotalDurationMinutes { get; set; }
    public int MovingDurationMinutes { get; set; }
    public int StationaryDurationMinutes { get; set; }
    public double AverageMovingSpeedKmh { get; set; }
    public double MaxSpeedKmh { get; set; }
    public int BatteryDrainPercentage { get; set; }
}

public class RouteStopDto
{
    public int StopIndex { get; set; }
    public string SiteName { get; set; } = "Client Site / Field Stop";
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public DateTime ArrivedAt { get; set; }
    public DateTime DepartedAt { get; set; }
    public int DwellDurationMinutes { get; set; }
    public string StopType { get; set; } = "FIELD_STOP";
}

public class RoutePointDto
{
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal? SpeedKmh { get; set; }
    public DateTime RecordedAt { get; set; }
}
