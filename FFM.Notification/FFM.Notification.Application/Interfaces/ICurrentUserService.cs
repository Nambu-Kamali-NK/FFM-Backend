namespace FFM.Notification.Application.Interfaces;

public interface ICurrentUserService
{
    int GetUserId();
    IEnumerable<string> GetRoles();
    bool IsInRole(string role);
}

