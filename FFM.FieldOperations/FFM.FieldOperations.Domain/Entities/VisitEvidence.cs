using System;
using System.Linq;

namespace FFM.FieldOperations.Domain.Entities
{
    public class VisitEvidence
    {
        public static readonly string[] AllowedEvidenceTypes = new[]
        {
            "PHOTO",
            "SIGNATURE",
            "DOCUMENT",
            "METER_READING",
            "INVOICE"
        };

        public int Id { get; private set; }
        public int VisitId { get; private set; }
        public int EvidenceTypeId { get; private set; } = 1;
        public string EvidenceType { get; private set; } = "PHOTO";
        public string? FileName { get; private set; }
        public string? FolderPath { get; private set; }
        public string FileUrl { get; private set; } = string.Empty;
        public string? ThumbnailUrl { get; private set; }
        public string? Remarks { get; private set; }
        public decimal? CapturedLatitude { get; private set; }
        public decimal? CapturedLongitude { get; private set; }
        public DateTime CapturedAt { get; private set; }
        public string? ReadingsJson { get; private set; }

        public Visit? Visit { get; private set; }
        public virtual EvidenceType? EvidenceTypeMaster { get; private set; }

        private VisitEvidence() { }

        public VisitEvidence(
            int visitId,
            int evidenceTypeId,
            string fileUrl,
            string? thumbnailUrl = null,
            string? remarks = null,
            decimal? capturedLatitude = null,
            decimal? capturedLongitude = null,
            DateTime? capturedAt = null,
            string? evidenceTypeCode = null,
            string? readingsJson = null,
            string? fileName = null,
            string? folderPath = null)
        {
            if (visitId <= 0)
                throw new ArgumentException("VisitId must be greater than zero.", nameof(visitId));

            if (evidenceTypeId <= 0)
                throw new ArgumentException("EvidenceTypeId must be greater than zero.", nameof(evidenceTypeId));

            if (string.IsNullOrWhiteSpace(fileUrl))
                throw new ArgumentException("FileUrl cannot be empty.", nameof(fileUrl));

            var trimmedFileUrl = fileUrl.Trim();
            if (trimmedFileUrl.Length > 500)
                throw new ArgumentException("FileUrl cannot exceed 500 characters.", nameof(fileUrl));

            if (!Uri.TryCreate(trimmedFileUrl, UriKind.Absolute, out var fileUri) ||
                (fileUri.Scheme != Uri.UriSchemeHttps && fileUri.Scheme != Uri.UriSchemeHttp))
            {
                throw new ArgumentException("FileUrl must be a valid absolute HTTP or HTTPS URL.", nameof(fileUrl));
            }

            if (!string.IsNullOrWhiteSpace(thumbnailUrl))
            {
                var trimmedThumbUrl = thumbnailUrl.Trim();
                if (trimmedThumbUrl.Length > 500)
                    throw new ArgumentException("ThumbnailUrl cannot exceed 500 characters.", nameof(thumbnailUrl));

                if (!Uri.TryCreate(trimmedThumbUrl, UriKind.Absolute, out var thumbUri) ||
                    (thumbUri.Scheme != Uri.UriSchemeHttps && thumbUri.Scheme != Uri.UriSchemeHttp))
                {
                    throw new ArgumentException("ThumbnailUrl must be a valid absolute HTTP or HTTPS URL.", nameof(thumbnailUrl));
                }
                ThumbnailUrl = trimmedThumbUrl;
            }

            if (!string.IsNullOrWhiteSpace(remarks))
            {
                var trimmedRemarks = remarks.Trim();
                if (trimmedRemarks.Length > 500)
                    throw new ArgumentException("Remarks cannot exceed 500 characters.", nameof(remarks));
                Remarks = trimmedRemarks;
            }

            if (capturedLatitude.HasValue && (capturedLatitude < -90m || capturedLatitude > 90m))
                throw new ArgumentException("CapturedLatitude must be between -90 and 90.", nameof(capturedLatitude));

            if (capturedLongitude.HasValue && (capturedLongitude < -180m || capturedLongitude > 180m))
                throw new ArgumentException("CapturedLongitude must be between -180 and 180.", nameof(capturedLongitude));

            VisitId = visitId;
            EvidenceTypeId = evidenceTypeId;
            EvidenceType = string.IsNullOrWhiteSpace(evidenceTypeCode) ? MapIdToCode(evidenceTypeId) : evidenceTypeCode.Trim().ToUpperInvariant();
            FileUrl = trimmedFileUrl;
            FileName = fileName?.Trim();
            FolderPath = folderPath?.Trim();
            CapturedLatitude = capturedLatitude;
            CapturedLongitude = capturedLongitude;
            CapturedAt = capturedAt.HasValue
                ? (capturedAt.Value.Kind == DateTimeKind.Utc ? capturedAt.Value : DateTime.SpecifyKind(capturedAt.Value, DateTimeKind.Utc))
                : DateTime.UtcNow;
            ReadingsJson = readingsJson;
        }

        private static string MapIdToCode(int id) => id switch
        {
            1 => "PHOTO",
            2 => "METER_READING",
            3 => "SIGNATURE",
            4 => "DOCUMENT",
            _ => "PHOTO"
        };

        private static int MapCodeToId(string code) => code.ToUpperInvariant() switch
        {
            "PHOTO" => 1,
            "METER_READING" => 2,
            "SIGNATURE" => 3,
            "DOCUMENT" => 4,
            "INVOICE" => 4,
            _ => 1
        };

        public VisitEvidence(
            int visitId,
            string evidenceType,
            string fileUrl,
            string? thumbnailUrl = null,
            string? remarks = null,
            decimal? capturedLatitude = null,
            decimal? capturedLongitude = null,
            DateTime? capturedAt = null)
            : this(
                visitId,
                MapCodeToId(evidenceType),
                fileUrl,
                thumbnailUrl,
                remarks,
                capturedLatitude,
                capturedLongitude,
                capturedAt,
                evidenceType)
        {
        }
    }
}

