using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFM.GPS.Tracking.Application.DTOs;
using FFM.GPS.Tracking.Domain.Entities;

namespace FFM.GPS.Tracking.Application.Services
{
    public interface IGeofenceBreachDetector
    {
        Task<List<GeofenceAlertDto>> EvaluateBreadcrumbAsync(LocationBreadcrumb breadcrumb, CancellationToken cancellationToken = default);
    }
}
