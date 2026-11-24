using System.ComponentModel.DataAnnotations;

namespace FFM.Workforce.Application.DTOs
{
    public class PermissionDecisionRequest
    {
        public int Id { get; set; }
        public int RequestId { get; set; }

        [Required]
        [RegularExpression("^(APPROVED|REJECTED)$", ErrorMessage = "Decision must be either 'APPROVED' or 'REJECTED'.")]
        public string Decision { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Comments { get; set; }
    }
}
