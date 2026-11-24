using System;
using System.Threading.Tasks;
using BCrypt.Net;
using FFM.Identity.Access.Api.Models;
using FFM.Identity.Access.Domain.Entities;
using FFM.Identity.Access.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FFM.Identity.Access.Api.Services
{
    public class UserService : IUserService
    {
        private readonly IdentityDbContext _dbContext;

        public UserService(IdentityDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<int?> CreateUserAsync(CreateUserRequest request)
        {
            var usernameOrEmail = request.UsernameOrEmail.Trim().ToLowerInvariant();

            var existingUser = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.UsernameOrEmail == usernameOrEmail);

            if (existingUser != null)
                return null;

            var user = new User
            {
                UsernameOrEmail = usernameOrEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = request.Role,
                Status = AccountStatus.PendingActivation,
                MfaEnabled = false,
                FailedAttempts = 0
            };

            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            return user.Id;
        }
    }
}