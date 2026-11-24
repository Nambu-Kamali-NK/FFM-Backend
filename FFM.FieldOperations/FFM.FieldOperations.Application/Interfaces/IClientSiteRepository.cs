using System.Collections.Generic;
using System.Threading.Tasks;
using FFM.FieldOperations.Domain.Entities;

namespace FFM.FieldOperations.Application.Interfaces
{
    public interface IClientSiteRepository
    {
        Task<ClientSite?> GetByIdAsync(int id);
        Task<ClientSite?> GetByCodeAsync(string siteCode);
        Task<bool> ExistsByCodeAsync(string siteCode);
        Task<List<ClientSite>> GetAllAsync(bool activeOnly = true);
        Task AddAsync(ClientSite clientSite);
        Task SaveChangesAsync();
    }

    public interface ICurrentUserService
    {
        int GetUserId();
        IEnumerable<string> GetRoles();
        bool IsInRole(string role);
    }
}
