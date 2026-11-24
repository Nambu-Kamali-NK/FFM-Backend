namespace FFM.Identity.Access.Api.Models
{
    public class MfaResendRequest
    {
        public string MfaChallengeId { get; set; } = null!;
    }
}
