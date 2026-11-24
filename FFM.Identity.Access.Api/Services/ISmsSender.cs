using System.Threading.Tasks;

namespace FFM.Identity.Access.Api.Services
{
    public interface ISmsSender
    {
        Task SendMfaCodeSmsAsync(string phoneNumberOrEmail, string verificationCode);
    }
}
