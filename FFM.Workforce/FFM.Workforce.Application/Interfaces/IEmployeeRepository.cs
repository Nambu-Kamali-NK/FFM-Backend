namespace FFM.Workforce.Application.Interfaces
{
    using FFM.Workforce.Domain.Entities;
    using System;
    using System.Threading.Tasks;
    using System.Collections.Generic;

    public interface IEmployeeRepository
    {
        Task<Employee?> GetByIdAsync(int employeeId);
        Task<List<Employee>> GetAllAsync();
        Task<Employee?> GetByUserIdAsync(int userId);
        Task<Employee?> GetByEmployeeCodeAsync(string employeeCode);
        Task<Employee?> GetByEmailAsync(string email);
        Task<Employee?> GetByMobileAsync(string mobile);
        Task<bool> ExistsAsync(int employeeId);
        Task<bool> IsEmailTakenByOtherAsync(string email, int excludeEmployeeId);
        Task<bool> IsMobileTakenByOtherAsync(string mobile, int excludeEmployeeId);
        Task<List<int>> GetManagerChainAsync(int managerId);
        Task AddAsync(Employee employee);
        Task SaveChangesAsync();
    }
}
