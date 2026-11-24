using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FFM.Identity.Access.Api.Services
{
    public class EmailSender : IEmailSender
    {
        private readonly ILogger<EmailSender> _logger;
        private readonly IConfiguration _config;

        public EmailSender(ILogger<EmailSender> logger, IConfiguration config)
        {
            _logger = logger;
            _config = config;
        }

        public async Task SendPasswordResetEmailAsync(string email, string resetToken)
        {
            var host = _config["Smtp:Host"];
            var portStr = _config["Smtp:Port"];
            var username = _config["Smtp:Username"];
            var password = _config["Smtp:Password"];
            var senderEmail = _config["Smtp:SenderEmail"] ?? "no-reply@muscat.gov.om";
            var senderName = _config["Smtp:SenderName"] ?? "Muscat Municipality FFM";

            var resetUrl = $"http://localhost:5173/?token={resetToken}";

            var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Password Reset Request</title>
</head>
<body style='font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Helvetica, Arial, sans-serif; background-color: #f3f7f7; margin: 0; padding: 24px; color: #1a2b2c;'>
    <div style='max-width: 560px; margin: 0 auto; background: #ffffff; border-radius: 16px; border: 1px solid #d8e4e5; overflow: hidden; box-shadow: 0 4px 12px rgba(0,0,0,0.05);'>
        <div style='background-color: #1C585C; padding: 24px; text-align: center; color: white;'>
            <h2 style='margin: 0; font-size: 20px; font-weight: 700;'>Muscat Municipality</h2>
            <p style='margin: 4px 0 0; font-size: 12px; color: #a3d9d2; letter-spacing: 1px; text-transform: uppercase;'>Field Force Management Portal</p>
        </div>
        <div style='padding: 32px 24px;'>
            <h3 style='margin-top: 0; color: #1a2b2c; font-size: 18px;'>Password Recovery Request</h3>
            <p style='font-size: 14px; line-height: 1.6; color: #5a7174;'>
                We received a request to reset your password for your account associated with <strong>{email}</strong>.
            </p>
            <div style='margin: 24px 0; padding: 16px; background-color: #f8fafc; border-radius: 12px; border: 1px dashed #cbd5e1; text-align: center;'>
                <p style='margin: 0 0 8px; font-size: 12px; color: #64748b; font-weight: 600; text-transform: uppercase;'>Your 64-Character Reset Token</p>
                <code style='display: block; font-family: monospace; font-size: 13px; color: #1C585C; word-break: break-all; font-weight: bold;'>{resetToken}</code>
            </div>
            <div style='text-align: center; margin: 28px 0;'>
                <a href='{resetUrl}' style='display: inline-block; background-color: #1C585C; color: #ffffff; padding: 12px 28px; font-size: 14px; font-weight: 600; text-decoration: none; border-radius: 10px;'>
                    Reset My Password
                </a>
            </div>
            <p style='font-size: 12px; color: #8fa6a8; line-height: 1.5; margin-top: 24px; border-top: 1px solid #edf2f7; padding-top: 16px;'>
                This token is cryptographically signed and will expire in 15 minutes. If you did not request this, please ignore this email or notify IT security.
            </p>
        </div>
    </div>
</body>
</html>";

            await SendEmailAsync(email, "Password Reset Request - Muscat Municipality FFM", htmlBody, resetToken);
        }

        public async Task SendMfaCodeEmailAsync(string email, string verificationCode)
        {
            var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Your 2FA Verification Code</title>
</head>
<body style='font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Helvetica, Arial, sans-serif; background-color: #f3f7f7; margin: 0; padding: 24px; color: #1a2b2c;'>
    <div style='max-width: 520px; margin: 0 auto; background: #ffffff; border-radius: 16px; border: 1px solid #d8e4e5; overflow: hidden; box-shadow: 0 4px 12px rgba(0,0,0,0.05);'>
        <div style='background-color: #1C585C; padding: 24px; text-align: center; color: white;'>
            <h2 style='margin: 0; font-size: 20px; font-weight: 700;'>Muscat Municipality</h2>
            <p style='margin: 4px 0 0; font-size: 12px; color: #a3d9d2; letter-spacing: 1px; text-transform: uppercase;'>Two-Factor Authentication (2FA)</p>
        </div>
        <div style='padding: 32px 24px; text-align: center;'>
            <h3 style='margin-top: 0; color: #1a2b2c; font-size: 18px;'>Login Verification Code</h3>
            <p style='font-size: 14px; line-height: 1.6; color: #5a7174;'>
                Use the 6-digit verification code below to complete your login to the Administrative Portal.
            </p>
            <div style='margin: 24px auto; padding: 18px 24px; background-color: #E6F4F1; border-radius: 14px; border: 2px solid #1C585C; max-width: 220px;'>
                <span style='font-family: monospace; font-size: 32px; font-weight: 800; letter-spacing: 8px; color: #1C585C;'>{verificationCode}</span>
            </div>
            <p style='font-size: 12px; color: #8fa6a8; margin-top: 20px;'>
                This code is valid for <strong>5 minutes</strong>. Do not share this code with anyone.
            </p>
        </div>
    </div>
</body>
</html>";

            await SendEmailAsync(email, "Your 2FA Verification Code: " + verificationCode, htmlBody, verificationCode);
        }

        private async Task SendEmailAsync(string recipient, string subject, string htmlBody, string codeOrToken)
        {
            var host = _config["Smtp:Host"];
            var portStr = _config["Smtp:Port"];
            var username = _config["Smtp:Username"];
            var password = _config["Smtp:Password"];
            var senderEmail = _config["Smtp:SenderEmail"] ?? "no-reply@muscat.gov.om";
            var senderName = _config["Smtp:SenderName"] ?? "Muscat Municipality FFM";

            // If SMTP is configured with credentials, dispatch via real SMTP client
            if (!string.IsNullOrWhiteSpace(host) && !string.IsNullOrWhiteSpace(username) && int.TryParse(portStr, out var port))
            {
                try
                {
                    using var message = new MailMessage
                    {
                        From = new MailAddress(senderEmail, senderName),
                        Subject = subject,
                        Body = htmlBody,
                        IsBodyHtml = true
                    };
                    message.To.Add(new MailAddress(recipient));

                    using var client = new SmtpClient(host, port)
                    {
                        Credentials = new NetworkCredential(username, password),
                        EnableSsl = true
                    };

                    await client.SendMailAsync(message);

                    _logger.LogInformation(
                        "[SMTP DISPATCH SUCCESS] Real email delivered to {Recipient} via {Host}:{Port}. Subject: '{Subject}'.",
                        recipient, host, port, subject);
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "[SMTP DISPATCH FAILED] Could not deliver email to {Recipient} via {Host}. Falling back to internal logger.",
                        recipient, host);
                }
            }

            // Fallback development logging
            _logger.LogInformation(
                "[EMAIL DISPATCH] Destination: {Recipient} | Subject: '{Subject}' | Code/Token: {CodeOrToken}",
                recipient, subject, codeOrToken);
        }
    }
}
