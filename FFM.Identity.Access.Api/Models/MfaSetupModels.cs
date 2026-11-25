using System;
using System.Collections.Generic;

namespace FFM.Identity.Access.Api.Models
{
    public class MfaSetupResponse
    {
        public string Secret { get; set; } = string.Empty;
        public string QrCodeUri { get; set; } = string.Empty;
        public string ManualEntryKey { get; set; } = string.Empty;
        public string Issuer { get; set; } = "FFM";
        public string Account { get; set; } = string.Empty;
    }

    public class MfaEnableRequest
    {
        public string Secret { get; set; } = string.Empty;
        public string VerificationCode { get; set; } = string.Empty;
    }

    public class MfaDisableRequest
    {
        public string Password { get; set; } = string.Empty;
        public string? VerificationCode { get; set; }
    }

    public class OAuthLoginRequest
    {
        public string Provider { get; set; } = "google"; // "google" or "microsoft"
        public string IdToken { get; set; } = string.Empty;
        public string? AccessToken { get; set; }
    }

    public class OAuthUserInfo
    {
        public string Email { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public string ProviderSubjectId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool EmailVerified { get; set; }
        public string? Picture { get; set; }
    }

    public class OAuthProviderDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public bool Enabled { get; set; }
    }
}
