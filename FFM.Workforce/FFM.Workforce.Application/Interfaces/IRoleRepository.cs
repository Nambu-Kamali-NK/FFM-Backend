using FFM.Workforce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace FFM.Workforce.Application.Interfaces;

/// <summary>
/// Read-only Role queries used by application handlers.
/// </summary>
public interface IRoleRepository
{
    Task<bool> ExistsAsync(int roleId);
}
