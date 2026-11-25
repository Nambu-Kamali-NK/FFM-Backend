using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace FFM.Identity.Access.Api.Services
{
    /// <summary>
    /// Implements RFC 6238 Time-Based One-Time Password (TOTP) algorithm
    /// for Authenticator Apps (Google Authenticator, Microsoft Authenticator).
    /// </summary>
    public static class TotpService
    {
        private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        private const int TimeStepSeconds = 30;
        private const int Digits = 6;

        /// <summary>
        /// Generates a cryptographically secure random Base32 secret key.
        /// </summary>
        public static string GenerateSecret(int byteLength = 20)
        {
            var bytes = new byte[byteLength];
            RandomNumberGenerator.Fill(bytes);
            return ToBase32String(bytes);
        }

        /// <summary>
        /// Computes the 6-digit TOTP code for a secret at a given timestamp.
        /// </summary>
        public static string GenerateTotpCode(string base32Secret, DateTimeOffset? timestamp = null)
        {
            var time = timestamp ?? DateTimeOffset.UtcNow;
            long counter = time.ToUnixTimeSeconds() / TimeStepSeconds;
            return ComputeTotp(base32Secret, counter);
        }

        /// <summary>
        /// Verifies a 6-digit code against a secret with a default tolerance window of +/- 30 seconds.
        /// </summary>
        public static bool VerifyTotpCode(string base32Secret, string code, int toleranceSteps = 1, DateTimeOffset? timestamp = null)
        {
            if (string.IsNullOrWhiteSpace(base32Secret) || string.IsNullOrWhiteSpace(code))
                return false;

            code = code.Trim();
            if (code.Length != Digits || !int.TryParse(code, out _))
                return false;

            var time = timestamp ?? DateTimeOffset.UtcNow;
            long currentCounter = time.ToUnixTimeSeconds() / TimeStepSeconds;

            for (int i = -toleranceSteps; i <= toleranceSteps; i++)
            {
                var expected = ComputeTotp(base32Secret, currentCounter + i);
                if (CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(expected),
                    Encoding.UTF8.GetBytes(code)))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Constructs an otpauth:// URI for scanning with authenticator apps.
        /// </summary>
        public static string GetQrCodeUri(string issuer, string userEmail, string base32Secret)
        {
            var encodedIssuer = Uri.EscapeDataString(issuer);
            var encodedUser = Uri.EscapeDataString(userEmail);
            return $"otpauth://totp/{encodedIssuer}:{encodedUser}?secret={base32Secret}&issuer={encodedIssuer}&algorithm=SHA1&digits={Digits}&period={TimeStepSeconds}";
        }

        private static string ComputeTotp(string base32Secret, long counter)
        {
            var key = FromBase32String(base32Secret);
            var counterBytes = BitConverter.GetBytes(counter);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(counterBytes);
            }

            using var hmac = new HMACSHA1(key);
            var hash = hmac.ComputeHash(counterBytes);

            int offset = hash[^1] & 0x0F;
            int binaryCode = ((hash[offset] & 0x7F) << 24)
                           | ((hash[offset + 1] & 0xFF) << 16)
                           | ((hash[offset + 2] & 0xFF) << 8)
                           | (hash[offset + 3] & 0xFF);

            int otp = binaryCode % 1000000;
            return otp.ToString("D6");
        }

        public static string ToBase32String(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return string.Empty;

            var sb = new StringBuilder((bytes.Length * 8 + 4) / 5);
            int buffer = 0;
            int bitsLeft = 0;

            foreach (var b in bytes)
            {
                buffer = (buffer << 8) | b;
                bitsLeft += 8;
                while (bitsLeft >= 5)
                {
                    bitsLeft -= 5;
                    sb.Append(Base32Alphabet[(buffer >> bitsLeft) & 0x1F]);
                }
            }

            if (bitsLeft > 0)
            {
                sb.Append(Base32Alphabet[(buffer << (5 - bitsLeft)) & 0x1F]);
            }

            return sb.ToString();
        }

        public static byte[] FromBase32String(string base32)
        {
            if (string.IsNullOrWhiteSpace(base32)) return Array.Empty<byte>();

            var upper = base32.Trim().ToUpperInvariant().Replace(" ", "").Replace("-", "");
            var output = new List<byte>();
            int buffer = 0;
            int bitsLeft = 0;

            foreach (var c in upper)
            {
                int val = Base32Alphabet.IndexOf(c);
                if (val < 0) continue;

                buffer = (buffer << 5) | val;
                bitsLeft += 5;

                if (bitsLeft >= 8)
                {
                    bitsLeft -= 8;
                    output.Add((byte)((buffer >> bitsLeft) & 0xFF));
                }
            }

            return output.ToArray();
        }
    }
}
