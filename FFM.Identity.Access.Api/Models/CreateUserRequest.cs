using System.ComponentModel.DataAnnotations;
using FFM.Identity.Access.Domain.Entities;

namespace FFM.Identity.Access.Api.Models
{
    public class CreateUserRequest
    {
        [Required(ErrorMessage = "Username or email is required.")]
        [StringLength(150, MinimumLength = 3, ErrorMessage = "Username or email must be between 3 and 150 characters.")]
        public string UsernameOrEmail { get; set; } = null!;

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters long.")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).+$", 
            ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, one number, and one special character.")]
        public string Password { get; set; } = null!;

        [Required]
        public Role Role { get; set; }
    }
}