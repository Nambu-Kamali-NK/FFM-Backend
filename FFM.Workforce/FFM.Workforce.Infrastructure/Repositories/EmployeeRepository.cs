using FFM.Workforce.Application.Interfaces;
using FFM.Workforce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace FFM.Workforce.Infrastructure.Repositories;

public class EmployeeRepository : IEmployeeRepository
{
    private readonly WorkforceDbContext _dbContext;

    public EmployeeRepository(WorkforceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Employee?> GetByIdAsync(int employeeId)
    {
        if (employeeId <= 0) return null;
        return await _dbContext.Employees.FirstOrDefaultAsync(e => e.Id == employeeId);
    }

    public async Task<List<Employee>> GetAllAsync()
    {
        return await _dbContext.Employees.AsNoTracking().ToListAsync();
    }

    public async Task<Employee?> GetByUserIdAsync(int userId)
    {
        // UserId is int
        if (userId <= 0) return null;
        return await _dbContext.Employees.FirstOrDefaultAsync(e => e.UserId == userId);
    }

    public async Task<Employee?> GetByEmployeeCodeAsync(string employeeCode)
    {
        if (string.IsNullOrWhiteSpace(employeeCode)) return null;
        return await _dbContext.Employees.FirstOrDefaultAsync(e => e.EmployeeCode == employeeCode);
    }

    public async Task<Employee?> GetByEmailAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var normalized = email.Trim().ToLower();
        return await _dbContext.Employees.FirstOrDefaultAsync(e => e.Email != null && e.Email.ToLower() == normalized);
    }

    public async Task<Employee?> GetByMobileAsync(string mobile)
    {
        if (string.IsNullOrWhiteSpace(mobile)) return null;
        var trimmed = mobile.Trim();
        return await _dbContext.Employees.FirstOrDefaultAsync(e => e.Mobile != null && e.Mobile == trimmed);
    }

    public async Task<bool> ExistsAsync(int employeeId)
    {
        if (employeeId <= 0) return false;
        return await _dbContext.Employees.AnyAsync(e => e.Id == employeeId);
    }

    public async Task<bool> IsEmailTakenByOtherAsync(string email, int excludeEmployeeId)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        var normalized = email.Trim().ToLower();
        return await _dbContext.Employees.AnyAsync(e => e.Id != excludeEmployeeId && e.Email != null && e.Email.ToLower() == normalized);
    }

    public async Task<bool> IsMobileTakenByOtherAsync(string mobile, int excludeEmployeeId)
    {
        if (string.IsNullOrWhiteSpace(mobile)) return false;
        var trimmed = mobile.Trim();
        return await _dbContext.Employees.AnyAsync(e => e.Id != excludeEmployeeId && e.Mobile != null && e.Mobile == trimmed);
    }

    public async Task<List<int>> GetManagerChainAsync(int managerId)
    {
        var chain = new List<int>();
        var currentId = (int?)managerId;

        while (currentId.HasValue && !chain.Contains(currentId.Value) && chain.Count < 50)
        {
            chain.Add(currentId.Value);
            var mgr = await _dbContext.Employees.AsNoTracking()
                .Where(e => e.Id == currentId.Value)
                .Select(e => e.ManagerId)
                .FirstOrDefaultAsync();

            currentId = mgr;
        }

        return chain;
    }

    public async Task AddAsync(Employee employee)
    {
        await _dbContext.Employees.AddAsync(employee);
    }

    public async Task SaveChangesAsync()
    {
        await _dbContext.SaveChangesAsync();
    }
}
