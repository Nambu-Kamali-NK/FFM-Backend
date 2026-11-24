using System;
using System.ComponentModel.DataAnnotations;

namespace FFM.FieldOperations.Application.DTOs
{
    public class CreateClientSiteRequest
    {
        [Required(ErrorMessage = "SiteCode is required.")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "SiteCode must be between 3 and 50 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9_\-]+$", ErrorMessage = "SiteCode may only contain alphanumeric characters, hyphens, and underscores.")]
        public string SiteCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Name is required.")]
        [StringLength(150, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 150 characters.")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? ContactPerson { get; set; }

        [MaxLength(30)]
        [Phone(ErrorMessage = "Invalid phone number format.")]
        public string? ContactPhone { get; set; }

        [MaxLength(150)]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string? ContactEmail { get; set; }

        [Required(ErrorMessage = "Latitude is required.")]
        [Range(-90.0, 90.0, ErrorMessage = "Latitude must be between -90.0 and +90.0.")]
        public decimal Latitude { get; set; }

        [Required(ErrorMessage = "Longitude is required.")]
        [Range(-180.0, 180.0, ErrorMessage = "Longitude must be between -180.0 and +180.0.")]
        public decimal Longitude { get; set; }

        [Range(20, 1000, ErrorMessage = "RadiusMeters must be between 20 and 1000 meters.")]
        public int RadiusMeters { get; set; } = 150;

        [Required(ErrorMessage = "Address is required.")]
        [StringLength(300, MinimumLength = 10, ErrorMessage = "Address must be at least 10 characters.")]
        public string Address { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? City { get; set; }

        [MaxLength(100)]
        public string? State { get; set; }

        [MaxLength(20)]
        public string? PostalCode { get; set; }
    }

    public class ClientSiteDto
    {
        public int Id { get; set; }
        public string SiteCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? ContactPerson { get; set; }
        public string? ContactPhone { get; set; }
        public string? ContactEmail { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public int RadiusMeters { get; set; }
        public string Address { get; set; } = string.Empty;
        public string? City { get; set; }
        public string? State { get; set; }
        public string? PostalCode { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
