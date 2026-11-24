using System;
using System.Collections.Generic;
using System.IO;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

var privateKeyBase64 = File.ReadAllText("./identity-private.key").Trim();
using var rsa = RSA.Create();
rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKeyBase64), out _);

var signingKey = new RsaSecurityKey(rsa)
{
    KeyId = "ffm-rsa-key-1"
};

var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256);

string GenerateToken(
    string userId, 
    string[] roles, 
    string clientId = "ffm-portal", 
    string issuer = "https://ffm.example.com", 
    string audience = "ffm-client", 
    DateTime? expires = null,
    SigningCredentials? customCredentials = null)
{
    var claims = new List<Claim>
    {
        new Claim(JwtRegisteredClaimNames.Sub, userId),
        new Claim("userId", userId),
        new Claim("user_id", userId),
        new Claim(ClaimTypes.NameIdentifier, userId),
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        new Claim("client_id", clientId)
    };

    foreach (var r in roles)
    {
        claims.Add(new Claim(ClaimTypes.Role, r));
        claims.Add(new Claim("role", r));
    }

    var tokenDescriptor = new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity(claims),
        NotBefore = (expires ?? DateTime.UtcNow).AddHours(-1),
        Expires = expires ?? DateTime.UtcNow.AddDays(30),
        Issuer = issuer,
        Audience = audience,
        SigningCredentials = customCredentials ?? credentials
    };

    var handler = new JwtSecurityTokenHandler();
    var token = handler.CreateToken(tokenDescriptor);
    return handler.WriteToken(token);
}

// Untrusted key
using var fakeRsa = RSA.Create(2048);
var fakeKey = new RsaSecurityKey(fakeRsa) { KeyId = "fake-key" };
var fakeCredentials = new SigningCredentials(fakeKey, SecurityAlgorithms.RsaSha256);

var tokens = new Dictionary<string, string>
{
    ["admin"] = GenerateToken("17", new[] { "ADMIN" }),
    ["system_admin"] = GenerateToken("1", new[] { "SYSTEM_ADMIN" }),
    ["manager"] = GenerateToken("10", new[] { "MANAGER" }),
    ["supervisor"] = GenerateToken("11", new[] { "SUPERVISOR" }),
    ["employee8"] = GenerateToken("8", new[] { "EMPLOYEE" }, clientId: "ffm-mobile"),
    ["employee12"] = GenerateToken("12", new[] { "EMPLOYEE" }, clientId: "ffm-mobile"),
    ["expired"] = GenerateToken("17", new[] { "ADMIN" }, expires: DateTime.UtcNow.AddHours(-1)),
    ["untrusted"] = GenerateToken("17", new[] { "ADMIN" }, customCredentials: fakeCredentials),
    ["wrong_issuer"] = GenerateToken("17", new[] { "ADMIN" }, issuer: "https://evil.com"),
    ["wrong_audience"] = GenerateToken("17", new[] { "ADMIN" }, audience: "evil-audience"),
    ["no_role"] = GenerateToken("17", Array.Empty<string>()),
    ["guest_role"] = GenerateToken("99", new[] { "VENDOR_GUEST" }),
    ["lowercase_employee"] = GenerateToken("12", new[] { "employee" }),
    ["multi_role"] = GenerateToken("12", new[] { "EMPLOYEE", "SUPERVISOR" })
};

Directory.CreateDirectory("scratch");
File.WriteAllText("scratch/test_tokens.json", JsonSerializer.Serialize(tokens, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine("TOKENS_GENERATED_SUCCESSFULLY");
