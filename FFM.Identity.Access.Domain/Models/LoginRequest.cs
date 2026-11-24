// File: LoginRequest.cs
namespace FFM.Identity.Access.Domain.Models
{
    public class LoginRequest
    {
        public string Username { get; set; } = null!; // email or username
        public string Password { get; set; } = null!;
        public string? DeviceId { get; set; }
    }
}
