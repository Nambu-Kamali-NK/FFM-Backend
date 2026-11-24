using FFM.Identity.Access.Domain.Entities;

namespace FFM.Identity.Access.Api.Models
{
    public class UserInfoResponse
    {
        public int Id { get; set; }
        public string UsernameOrEmail { get; set; } = null!;
        public bool MfaEnabled { get; set; }
        public AccountStatus Status { get; set; }
    }
}
