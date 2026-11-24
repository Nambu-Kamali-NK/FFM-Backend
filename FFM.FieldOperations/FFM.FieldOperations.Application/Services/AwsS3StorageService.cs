using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FFM.FieldOperations.Application.Interfaces;

namespace FFM.FieldOperations.Application.Services
{
    public class AwsS3StorageService : IS3StorageService
    {
        private readonly string _bucketName;
        private readonly string _region;
        private readonly string? _accessKey;
        private readonly string? _secretKey;
        private readonly int _defaultExpiryMinutes;

        private static readonly HashSet<string> DisallowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".bat", ".cmd", ".sh", ".ps1", ".vbs", ".dll", ".com", ".scr", ".msi"
        };

        public AwsS3StorageService(
            string bucketName = "ffm-field-evidence-prod",
            string region = "ap-south-1",
            string? accessKey = null,
            string? secretKey = null,
            int defaultExpiryMinutes = 15)
        {
            _bucketName = string.IsNullOrWhiteSpace(bucketName) ? "ffm-field-evidence-prod" : bucketName;
            _region = string.IsNullOrWhiteSpace(region) ? "ap-south-1" : region;
            _accessKey = accessKey;
            _secretKey = secretKey;
            _defaultExpiryMinutes = defaultExpiryMinutes > 0 ? defaultExpiryMinutes : 15;
        }

        public PreSignedUploadUrlItem GeneratePreSignedUploadUrl(
            int visitId,
            string fileName,
            string? contentType = null,
            TimeSpan? expiry = null)
        {
            if (visitId <= 0)
                throw new ArgumentException("VisitId must be greater than zero.", nameof(visitId));

            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("FileName cannot be empty.", nameof(fileName));

            var sanitizedFileName = Path.GetFileName(fileName.Trim());
            var extension = Path.GetExtension(sanitizedFileName);

            if (DisallowedExtensions.Contains(extension))
            {
                throw new ArgumentException($"File type '{extension}' is not permitted for security reasons.", nameof(fileName));
            }

            var resolvedContentType = !string.IsNullOrWhiteSpace(contentType)
                ? contentType.Trim()
                : GetDefaultContentType(extension);

            var expirySpan = expiry ?? TimeSpan.FromMinutes(_defaultExpiryMinutes);
            var expirySeconds = (int)expirySpan.TotalSeconds;
            var nowUtc = DateTime.UtcNow;
            var expiresAtUtc = nowUtc.Add(expirySpan);

            // Construct unique key
            var uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8);
            var safeName = Path.GetFileNameWithoutExtension(sanitizedFileName);
            var objectKey = $"visits/{visitId}/{uniqueId}_{safeName}{extension}";

            var host = $"{_bucketName}.s3.{_region}.amazonaws.com";
            var fileUrl = $"https://{host}/{objectKey}";

            string uploadUrl;
            if (!string.IsNullOrWhiteSpace(_accessKey) && !string.IsNullOrWhiteSpace(_secretKey))
            {
                uploadUrl = BuildSigV4Url(host, objectKey, resolvedContentType, expirySeconds, nowUtc);
            }
            else
            {
                // Development / Mock AWS S3 Compliant URL
                var signature = Guid.NewGuid().ToString("N");
                uploadUrl = $"https://{host}/{objectKey}?X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Credential=MOCKKEY%2F{nowUtc:yyyyMMdd}%2F{_region}%2Fs3%2Faws4_request&X-Amz-Date={nowUtc:yyyyMMddTHHmmssZ}&X-Amz-Expires={expirySeconds}&X-Amz-SignedHeaders=host&X-Amz-Signature={signature}";
            }

            return new PreSignedUploadUrlItem
            {
                FileName = sanitizedFileName,
                ContentType = resolvedContentType,
                UploadUrl = uploadUrl,
                FileUrl = fileUrl,
                ExpiresAtUtc = expiresAtUtc,
                ExpiresInMinutes = (int)expirySpan.TotalMinutes
            };
        }

        public IReadOnlyList<PreSignedUploadUrlItem> GenerateBatchPreSignedUploadUrls(
            int visitId,
            IEnumerable<(string FileName, string? ContentType)> files,
            TimeSpan? expiry = null)
        {
            if (files == null)
                throw new ArgumentNullException(nameof(files));

            var list = new List<PreSignedUploadUrlItem>();
            foreach (var file in files)
            {
                if (!string.IsNullOrWhiteSpace(file.FileName))
                {
                    list.Add(GeneratePreSignedUploadUrl(visitId, file.FileName, file.ContentType, expiry));
                }
            }

            return list;
        }

        private string BuildSigV4Url(string host, string objectKey, string contentType, int expirySeconds, DateTime nowUtc)
        {
            var dateStamp = nowUtc.ToString("yyyyMMdd");
            var amzDate = nowUtc.ToString("yyyyMMddTHHmmssZ");
            var credentialScope = $"{dateStamp}/{_region}/s3/aws4_request";
            var credentialParam = Uri.EscapeDataString($"{_accessKey}/{credentialScope}");

            var canonicalUri = $"/{objectKey}";
            var canonicalQueryString = $"X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Credential={credentialParam}&X-Amz-Date={amzDate}&X-Amz-Expires={expirySeconds}&X-Amz-SignedHeaders=host";
            var canonicalHeaders = $"host:{host}\n";
            var signedHeaders = "host";
            var payloadHash = "UNSIGNED-PAYLOAD";

            var canonicalRequest = $"PUT\n{canonicalUri}\n{canonicalQueryString}\n{canonicalHeaders}\n{signedHeaders}\n{payloadHash}";

            using var sha256 = SHA256.Create();
            var canonicalRequestHash = ToHexString(sha256.ComputeHash(Encoding.UTF8.GetBytes(canonicalRequest)));

            var stringToSign = $"AWS4-HMAC-SHA256\n{amzDate}\n{credentialScope}\n{canonicalRequestHash}";

            var signingKey = GetSignatureKey(_secretKey!, dateStamp, _region, "s3");
            using var hmac = new HMACSHA256(signingKey);
            var signature = ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));

            return $"https://{host}{canonicalUri}?{canonicalQueryString}&X-Amz-Signature={signature}";
        }

        private static byte[] GetSignatureKey(string key, string dateStamp, string regionName, string serviceName)
        {
            var kSecret = Encoding.UTF8.GetBytes("AWS4" + key);
            var kDate = HmacSha256(kSecret, dateStamp);
            var kRegion = HmacSha256(kDate, regionName);
            var kService = HmacSha256(kRegion, serviceName);
            return HmacSha256(kService, "aws4_request");
        }

        private static byte[] HmacSha256(byte[] key, string data)
        {
            using var hmac = new HMACSHA256(key);
            return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        }

        private static string ToHexString(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }

        private static string GetDefaultContentType(string extension) => extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".pdf" => "application/pdf",
            ".webp" => "image/webp",
            ".mp4" => "video/mp4",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            _ => "application/octet-stream"
        };
    }
}
