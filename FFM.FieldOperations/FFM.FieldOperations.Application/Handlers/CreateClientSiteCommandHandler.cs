using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFM.FieldOperations.Application.Commands;
using FFM.FieldOperations.Application.DTOs;
using FFM.FieldOperations.Application.Interfaces;
using FFM.FieldOperations.Domain.Entities;
using MediatR;

namespace FFM.FieldOperations.Application.Handlers
{
    public class CreateClientSiteCommandHandler : IRequestHandler<CreateClientSiteCommand, ClientSiteDto>
    {
        private readonly IClientSiteRepository _repository;
        private readonly ICurrentUserService _currentUserService;

        public CreateClientSiteCommandHandler(
            IClientSiteRepository repository,
            ICurrentUserService currentUserService)
        {
            _repository = repository;
            _currentUserService = currentUserService;
        }

        public async Task<ClientSiteDto> Handle(CreateClientSiteCommand request, CancellationToken cancellationToken)
        {
            // 1. RBAC Guard: Only ADMIN, SYSTEM_ADMIN, and MANAGER roles are permitted to register sites
            var roles = _currentUserService.GetRoles().ToList();
            bool isAuthorized = roles.Any(r =>
                r.Equals("ADMIN", StringComparison.OrdinalIgnoreCase) ||
                r.Equals("SYSTEM_ADMIN", StringComparison.OrdinalIgnoreCase) ||
                r.Equals("SYSTEM ADMIN", StringComparison.OrdinalIgnoreCase) ||
                r.Equals("MANAGER", StringComparison.OrdinalIgnoreCase));

            if (!isAuthorized)
            {
                throw new UnauthorizedAccessException("Only Admins and Managers are authorized to register client sites.");
            }

            // 2. Uniqueness Guard (BR-SITE-01)
            bool exists = await _repository.ExistsByCodeAsync(request.SiteCode);
            if (exists)
            {
                throw new InvalidOperationException($"A client site with code '{request.SiteCode.Trim().ToUpperInvariant()}' already exists.");
            }

            // 3. Coordinate Bounds & Null Island Guard (BR-SITE-02, BR-SITE-03, BR-SITE-04)
            if (request.Latitude < -90.0m || request.Latitude > 90.0m)
            {
                throw new ArgumentOutOfRangeException(nameof(request.Latitude), "Latitude must be between -90.0 and +90.0.");
            }

            if (request.Longitude < -180.0m || request.Longitude > 180.0m)
            {
                throw new ArgumentOutOfRangeException(nameof(request.Longitude), "Longitude must be between -180.0 and +180.0.");
            }

            if (request.Latitude == 0.0m && request.Longitude == 0.0m)
            {
                throw new ArgumentException("Invalid coordinates: (0.0, 0.0) is not a valid client site location.");
            }

            // 4. Geofence Radius Bounds (BR-SITE-05)
            int radius = request.RadiusMeters <= 0 ? 150 : request.RadiusMeters;
            if (radius < 20 || radius > 1000)
            {
                throw new ArgumentOutOfRangeException(nameof(request.RadiusMeters), "RadiusMeters must be between 20 and 1000 meters.");
            }

            // 5. Address Completeness Guard (BR-SITE-07)
            if (string.IsNullOrWhiteSpace(request.Address) || request.Address.Trim().Length < 10)
            {
                throw new ArgumentException("Address must be at least 10 characters.");
            }

            // 6. Instantiate Domain Entity
            var site = new ClientSite(
                siteCode: request.SiteCode,
                name: request.Name,
                latitude: request.Latitude,
                longitude: request.Longitude,
                address: request.Address,
                createdByUserId: request.ActorUserId,
                radiusMeters: radius,
                contactPerson: request.ContactPerson,
                contactPhone: request.ContactPhone,
                contactEmail: request.ContactEmail,
                city: request.City,
                state: request.State,
                postalCode: request.PostalCode);

            // 7. Persist
            await _repository.AddAsync(site);
            await _repository.SaveChangesAsync();

            return new ClientSiteDto
            {
                Id = site.Id,
                SiteCode = site.SiteCode,
                Name = site.Name,
                ContactPerson = site.ContactPerson,
                ContactPhone = site.ContactPhone,
                ContactEmail = site.ContactEmail,
                Latitude = site.Latitude,
                Longitude = site.Longitude,
                RadiusMeters = site.RadiusMeters,
                Address = site.Address,
                City = site.City,
                State = site.State,
                PostalCode = site.PostalCode,
                IsActive = site.IsActive,
                CreatedAt = site.CreatedAt
            };
        }
    }
}
