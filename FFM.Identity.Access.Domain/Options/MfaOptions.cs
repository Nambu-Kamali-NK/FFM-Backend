// File: MfaOptions.cs
namespace FFM.Identity.Access.Domain.Options
{
    public class MfaOptions
    {
        public int OtpLength { get; set; } = 6;
        public int OtpTtlSeconds { get; set; } = 300;
        public int MaxInvalidAttempts { get; set; } = 5;
    }
}
