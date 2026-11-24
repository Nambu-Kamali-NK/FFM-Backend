using System.Threading.Tasks;
using FFM.GPS.Tracking.Application.DTOs;

namespace FFM.GPS.Tracking.Api.Hubs;

public interface ITrackingHubMethods
{
    Task JoinTeamStream(int teamId);
    Task LeaveTeamStream(int teamId);
    Task TrackUser(int targetUserId);
    Task UntrackUser(int targetUserId);
    Task<ApiResponse<BreadcrumbResponseDto>> SendBreadcrumb(CreateBreadcrumbRequest request);
}

