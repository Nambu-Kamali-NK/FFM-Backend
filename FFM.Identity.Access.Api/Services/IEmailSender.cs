using System.Threading.Tasks;

namespace FFM.Identity.Access.Api.Services
{
    public interface IEmailSender
    {
        Task SendPasswordResetEmailAsync(string email, string resetToken);
        Task SendMfaCodeEmailAsync(string email, string verificationCode);
    }
}
