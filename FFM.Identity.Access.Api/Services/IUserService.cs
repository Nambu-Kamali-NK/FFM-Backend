using System;
using System.Threading.Tasks;
using FFM.Identity.Access.Api.Models;

namespace FFM.Identity.Access.Api.Services
{
    public interface IUserService
    {
        Task<int?> CreateUserAsync(CreateUserRequest request);
    }
}