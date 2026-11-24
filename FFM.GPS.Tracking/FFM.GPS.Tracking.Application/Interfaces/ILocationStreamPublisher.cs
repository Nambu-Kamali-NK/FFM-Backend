using System.Threading;
using System.Threading.Tasks;
using FFM.GPS.Tracking.Application.DTOs;

namespace FFM.GPS.Tracking.Application.Interfaces;

public interface ILocationStreamPublisher
{
    Task PublishLocationAsync(LocationBreadcrumbStreamDto breadcrumb, CancellationToken cancellationToken = default);
    Task PublishUserOfflineAsync(UserOfflineEventDto offlineEvent, CancellationToken cancellationToken = default);
    Task PublishGeofenceAlertAsync(GeofenceAlertDto alert, CancellationToken cancellationToken = default);
}
