using FFM.FieldOperations.Application.DTOs;
using MediatR;

namespace FFM.FieldOperations.Application.Commands
{
    public class CreateClientSiteCommand : IRequest<ClientSiteDto>
    {
        public int ActorUserId { get; set; }
        public string SiteCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? ContactPerson { get; set; }
        public string? ContactPhone { get; set; }
        public string? ContactEmail { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public int RadiusMeters { get; set; } = 150;
        public string Address { get; set; } = string.Empty;
        public string? City { get; set; }
        public string? State { get; set; }
        public string? PostalCode { get; set; }
    }
}
