using System;
using System.Collections.Generic;
using System.Linq;
using FFM.FieldOperations.Application.DTOs;
using MediatR;

namespace FFM.FieldOperations.Application.Commands
{
    public class CheckOutVisitCommand : IRequest<VisitCheckOutResultDto>
    {
        public int? VisitId { get; set; }
        public int? TaskId { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public string CompletionNotes { get; set; } = string.Empty;
        public string? CustomerName { get; set; }
        public string? CustomerSignatureUrl { get; set; }
        public int? CustomerRating { get; set; }
        public int ActorUserId { get; set; }
        public IEnumerable<string> ActorRoles { get; set; } = Enumerable.Empty<string>();
    }
}

