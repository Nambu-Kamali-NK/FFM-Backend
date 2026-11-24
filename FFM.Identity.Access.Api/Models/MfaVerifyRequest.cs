// File: DTOs/MfaVerifyRequest.cs
namespace FFM.Identity.Access.Api.Models
{
    public class MfaVerifyRequest
    {
        public string MfaChallengeId { get; set; } = null!;
        public string VerificationCode { get; set; } = null!;
    }
}
