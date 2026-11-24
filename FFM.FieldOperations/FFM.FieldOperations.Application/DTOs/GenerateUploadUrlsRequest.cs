using System.Collections.Generic;

namespace FFM.FieldOperations.Application.DTOs
{
    public class UploadFileItemRequest
    {
        public string FileName { get; set; } = string.Empty;
        public string? ContentType { get; set; }
    }

    public class GenerateUploadUrlsRequest
    {
        public int VisitId { get; set; }
        public List<UploadFileItemRequest>? Files { get; set; }
        public string? FileName { get; set; }
        public string? ContentType { get; set; }
    }

    public class GenerateUploadUrlsResponse
    {
        public int VisitId { get; set; }
        public IReadOnlyList<FFM.FieldOperations.Application.Interfaces.PreSignedUploadUrlItem> UploadUrls { get; set; } 
            = new List<FFM.FieldOperations.Application.Interfaces.PreSignedUploadUrlItem>();
    }
}

