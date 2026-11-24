using System;
using System.Threading;
using System.Threading.Tasks;
using FFM.GPS.Tracking.Application.DTOs;
using FFM.GPS.Tracking.Application.Interfaces;
using FFM.GPS.Tracking.Domain.Entities;
using FFM.GPS.Tracking.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FFM.GPS.Tracking.Application.Commands;

public class CreateBreadcrumbCommand : IRequest<BreadcrumbResponseDto>
{
    public int UserId { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal AccuracyMeters { get; set; }
    public decimal? SpeedKmh { get; set; }
    public decimal? HeadingDegrees { get; set; }
    public decimal? AltitudeMeters { get; set; }
    public int? BatteryPercentage { get; set; }
    public string? ActivityType { get; set; }
    public DateTime RecordedAt { get; set; }
}

public class CreateBreadcrumbCommandHandler : IRequestHandler<CreateBreadcrumbCommand, BreadcrumbResponseDto>
{
    private readonly IBreadcrumbRepository _breadcrumbRepository;
    private readonly ILocationStreamPublisher? _streamPublisher;
    private readonly FFM.GPS.Tracking.Application.Services.IGeofenceBreachDetector? _breachDetector;
    private readonly Microsoft.Extensions.Logging.ILogger<CreateBreadcrumbCommandHandler>? _logger;

    public CreateBreadcrumbCommandHandler(IBreadcrumbRepository breadcrumbRepository)
        : this(breadcrumbRepository, null, null, null)
    {
    }

    public CreateBreadcrumbCommandHandler(
        IBreadcrumbRepository breadcrumbRepository,
        ILocationStreamPublisher? streamPublisher)
        : this(breadcrumbRepository, streamPublisher, null, null)
    {
    }

    public CreateBreadcrumbCommandHandler(
        IBreadcrumbRepository breadcrumbRepository,
        ILocationStreamPublisher? streamPublisher,
        Microsoft.Extensions.Logging.ILogger<CreateBreadcrumbCommandHandler>? logger)
        : this(breadcrumbRepository, streamPublisher, null, logger)
    {
    }

    public CreateBreadcrumbCommandHandler(
        IBreadcrumbRepository breadcrumbRepository,
        ILocationStreamPublisher? streamPublisher,
        FFM.GPS.Tracking.Application.Services.IGeofenceBreachDetector? breachDetector,
        Microsoft.Extensions.Logging.ILogger<CreateBreadcrumbCommandHandler>? logger = null)
    {
        _breadcrumbRepository = breadcrumbRepository;
        _streamPublisher = streamPublisher;
        _breachDetector = breachDetector;
        _logger = logger;
    }

    public async Task<BreadcrumbResponseDto> Handle(CreateBreadcrumbCommand request, CancellationToken cancellationToken)
    {
        if (request.Latitude < -90m || request.Latitude > 90m || request.Longitude < -180m || request.Longitude > 180m)
        {
            throw new ArgumentException("Latitude must be between -90 and +90, Longitude between -180 and +180.");
        }

        if (request.AccuracyMeters > 100m)
        {
            throw new ArgumentException("Accuracy radius exceeds acceptable threshold (100m).");
        }

        var recordedAt = request.RecordedAt == default ? DateTime.UtcNow : request.RecordedAt;

        if (recordedAt > DateTime.UtcNow.AddMinutes(10))
        {
            throw new ArgumentException("Recorded timestamp cannot be in the future.");
        }

        if (recordedAt < DateTime.UtcNow.AddDays(-7))
        {
            throw new ArgumentException("Recorded timestamp cannot be older than 7 days.");
        }

        var breadcrumb = new LocationBreadcrumb
        {
            UserId = request.UserId,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            AccuracyMeters = request.AccuracyMeters,
            SpeedKmh = request.SpeedKmh,
            HeadingDegrees = request.HeadingDegrees,
            AltitudeMeters = request.AltitudeMeters,
            BatteryPercentage = request.BatteryPercentage,
            ActivityType = request.ActivityType ?? "UNKNOWN",
            RecordedAt = recordedAt,
            CreatedAt = DateTime.UtcNow
        };

        await _breadcrumbRepository.AddAsync(breadcrumb, cancellationToken);
        await _breadcrumbRepository.SaveChangesAsync(cancellationToken);

        // Fast Path: Stream to Redis Cache & SignalR Real-Time Clients
        if (_streamPublisher != null)
        {
            try
            {
                var streamDto = new LocationBreadcrumbStreamDto
                {
                    UserId = breadcrumb.UserId,
                    Latitude = breadcrumb.Latitude,
                    Longitude = breadcrumb.Longitude,
                    AccuracyMeters = breadcrumb.AccuracyMeters,
                    SpeedKmh = breadcrumb.SpeedKmh,
                    HeadingDegrees = breadcrumb.HeadingDegrees,
                    AltitudeMeters = breadcrumb.AltitudeMeters,
                    BatteryPercentage = breadcrumb.BatteryPercentage,
                    ActivityType = breadcrumb.ActivityType,
                    RecordedAt = breadcrumb.RecordedAt,
                    TransmittedAt = breadcrumb.CreatedAt
                };

                await _streamPublisher.PublishLocationAsync(streamDto, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to stream live location event to Redis/SignalR for user {UserId}", breadcrumb.UserId);
                // Resilience: never fail cold-path database persistence if real-time streaming encounters transient issue
            }
        }

        // Automated Geofence Breach Detection & Real-Time Alerts
        if (_breachDetector != null)
        {
            try
            {
                await _breachDetector.EvaluateBreadcrumbAsync(breadcrumb, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Automated geofence breach evaluation failed for user {UserId}", breadcrumb.UserId);
                // Resilience: never fail breadcrumb ingestion if breach evaluation encounters transient issue
            }
        }

        return new BreadcrumbResponseDto
        {
            BreadcrumbId = breadcrumb.Id,
            UserId = breadcrumb.UserId,
            RecordedAt = breadcrumb.RecordedAt,
            ReceivedAt = breadcrumb.CreatedAt
        };
    }
}
