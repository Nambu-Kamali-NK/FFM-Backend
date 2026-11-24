using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace FFM.Identity.Access.Api.Services
{
    public class SmsSender : ISmsSender
    {
        private readonly ILogger<SmsSender> _logger;

        public SmsSender(ILogger<SmsSender> logger)
        {
            _logger = logger;
        }

        public Task SendMfaCodeSmsAsync(string phoneNumberOrEmail, string verificationCode)
        {
            _logger.LogInformation(
                "[SMS GATEWAY DISPATCH] Destination: {Destination} | Message: 'Muscat Municipality FFM Verification Code: {Code}. Valid for 5 minutes. Do not share.'",
                phoneNumberOrEmail, verificationCode);

            return Task.CompletedTask;
        }
    }
}
