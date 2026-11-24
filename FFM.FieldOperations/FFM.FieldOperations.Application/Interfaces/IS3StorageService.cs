using System;
using System.Collections.Generic;

namespace FFM.FieldOperations.Application.Interfaces
{
    public class PreSignedUploadUrlItem
    {
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public string UploadUrl { get; set; } = string.Empty;
        public string FileUrl { get; set; } = string.Empty;
        public DateTime ExpiresAtUtc { get; set; }
        public int ExpiresInMinutes { get; set; }
    }

    public interface IS3StorageService
    {
        PreSignedUploadUrlItem GeneratePreSignedUploadUrl(
            int visitId,
            string fileName,
            string? contentType = null,
            TimeSpan? expiry = null);

        IReadOnlyList<PreSignedUploadUrlItem> GenerateBatchPreSignedUploadUrls(
            int visitId,
            IEnumerable<(string FileName, string? ContentType)> files,
            TimeSpan? expiry = null);
    }
}

