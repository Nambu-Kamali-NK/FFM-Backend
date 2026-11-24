using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace FFM.Identity.Access.Api.Services
{
    public static class TokenHasher
    {
        /// <summary>
        /// Computes a SHA‑256 hash of the input string and returns a Base64 representation.
        /// </summary>
        public static string ComputeHash(string input)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = sha.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
    }
}
