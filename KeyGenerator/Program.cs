using System;
using System.IO;
using System.Security.Cryptography;

using var rsa = RSA.Create(2048);

var privateKey = Convert.ToBase64String(rsa.ExportPkcs8PrivateKey());
var publicKey = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());

File.WriteAllText("../identity-private.key", privateKey);
File.WriteAllText("../identity-public.key", publicKey);

Console.WriteLine("RSA key pair generated successfully.");
Console.WriteLine("Private key: ../identity-private.key");
Console.WriteLine("Public key : ../identity-public.key");
