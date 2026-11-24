using System.Threading.Tasks;
using FFM.GPS.Tracking.Application.DTOs;

namespace FFM.GPS.Tracking.Api.Hubs;

public interface ITrackingClient
{
    Task ReceiveLocationUpdate(LocationBreadcrumbStreamDto breadcrumb);
    Task UserWentOffline(UserOfflineEventDto offlineEvent);
    Task ReceiveGeofenceAlert(GeofenceAlertDto alert);
}
