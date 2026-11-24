using System;

namespace FFM.FieldOperations.Domain.Entities
{
    public class ClientSite
    {
        public int Id { get; private set; }
        public string SiteCode { get; private set; } = default!;
        public string Name { get; private set; } = default!;
        public string? ContactPerson { get; private set; }
        public string? ContactPhone { get; private set; }
        public string? ContactEmail { get; private set; }
        public decimal Latitude { get; private set; }
        public decimal Longitude { get; private set; }
        public int RadiusMeters { get; private set; }
        public string Address { get; private set; } = default!;
        public string? City { get; private set; }
        public string? State { get; private set; }
        public string? PostalCode { get; private set; }
        public bool IsActive { get; private set; }
        public int CreatedByUserId { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        private ClientSite() { }

        public ClientSite(
            string siteCode,
            string name,
            decimal latitude,
            decimal longitude,
            string address,
            int createdByUserId,
            int radiusMeters = 150,
            string? contactPerson = null,
            string? contactPhone = null,
            string? contactEmail = null,
            string? city = null,
            string? state = null,
            string? postalCode = null)
        {
            if (string.IsNullOrWhiteSpace(siteCode))
                throw new ArgumentException("SiteCode is required.", nameof(siteCode));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name is required.", nameof(name));
            if (string.IsNullOrWhiteSpace(address) || address.Trim().Length < 10)
                throw new ArgumentException("Address must be at least 10 characters.", nameof(address));
            if (latitude < -90.0m || latitude > 90.0m)
                throw new ArgumentOutOfRangeException(nameof(latitude), "Latitude must be between -90.0 and +90.0.");
            if (longitude < -180.0m || longitude > 180.0m)
                throw new ArgumentOutOfRangeException(nameof(longitude), "Longitude must be between -180.0 and +180.0.");
            if (latitude == 0.0m && longitude == 0.0m)
                throw new ArgumentException("Invalid coordinates: 0.0, 0.0 is not allowed.", nameof(latitude));
            if (radiusMeters < 20 || radiusMeters > 1000)
                throw new ArgumentOutOfRangeException(nameof(radiusMeters), "RadiusMeters must be between 20 and 1000.");
            if (createdByUserId <= 0)
                throw new ArgumentException("Valid CreatedByUserId is required.", nameof(createdByUserId));

            SiteCode = siteCode.Trim().ToUpperInvariant();
            Name = name.Trim();
            Latitude = Math.Round(latitude, 7);
            Longitude = Math.Round(longitude, 7);
            Address = address.Trim();
            RadiusMeters = radiusMeters;
            ContactPerson = contactPerson?.Trim();
            ContactPhone = contactPhone?.Trim();
            ContactEmail = contactEmail?.Trim();
            City = city?.Trim();
            State = state?.Trim();
            PostalCode = postalCode?.Trim();
            IsActive = true;
            CreatedByUserId = createdByUserId;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Deactivate()
        {
            IsActive = false;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Activate()
        {
            IsActive = true;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
