using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFM.FieldOperations.Application.DTOs;
using FFM.FieldOperations.Application.Queries;
using MediatR;

namespace FFM.FieldOperations.Application.Handlers
{
    public class GetTaskCancellationSchemaQueryHandler : IRequestHandler<GetTaskCancellationSchemaQuery, TaskCancellationSchemaDto>
    {
        public Task<TaskCancellationSchemaDto> Handle(GetTaskCancellationSchemaQuery request, CancellationToken cancellationToken)
        {
            var schema = new TaskCancellationSchemaDto
            {
                Title = "Task Cancellation Questionnaire",
                Description = "Select a verified cancellation reason and provide necessary supporting details.",
                Reasons = new List<CancellationReasonOptionDto>
                {
                    new CancellationReasonOptionDto
                    {
                        ReasonCode = "CLIENT_UNAVAILABLE",
                        Label = "Client / Facility Contact Unavailable",
                        Description = "Client facility was locked or no authorized representative was present on-site.",
                        RequiresFollowUpNote = true,
                        FollowUpPlaceholder = "Describe who you attempted to reach and phone call timestamps",
                        RequiresPhotoProof = true
                    },
                    new CancellationReasonOptionDto
                    {
                        ReasonCode = "SITE_INACCESSIBLE",
                        Label = "Physical Access Restricted / Road Block",
                        Description = "Security refused entry, road blocked, or hazardous environment.",
                        RequiresFollowUpNote = true,
                        FollowUpPlaceholder = "Describe physical obstruction or security checkpoint denial reason",
                        RequiresPhotoProof = true
                    },
                    new CancellationReasonOptionDto
                    {
                        ReasonCode = "SPARE_PARTS_UNAVAILABLE",
                        Label = "Specialized Replacement Parts Unavailable",
                        Description = "Required OEM replacement components or refrigerants are out of stock.",
                        RequiresFollowUpNote = true,
                        FollowUpPlaceholder = "Specify missing part numbers and required lead time",
                        RequiresPhotoProof = false
                    },
                    new CancellationReasonOptionDto
                    {
                        ReasonCode = "SEVERE_WEATHER",
                        Label = "Severe Weather / Safety Hazard",
                        Description = "Extreme rain, lightning, or rooftop conditions deemed hazardous to technician safety.",
                        RequiresFollowUpNote = true,
                        FollowUpPlaceholder = "Describe weather severity preventing safe servicing",
                        RequiresPhotoProof = false
                    },
                    new CancellationReasonOptionDto
                    {
                        ReasonCode = "CLIENT_REQUESTED_RESCHEDULE",
                        Label = "Client Requested Cancellation or Reschedule",
                        Description = "Customer contacted dispatch or technician directly requesting another date.",
                        RequiresFollowUpNote = true,
                        FollowUpPlaceholder = "Enter client representative name and preferred reschedule window",
                        RequiresPhotoProof = false
                    },
                    new CancellationReasonOptionDto
                    {
                        ReasonCode = "OTHER",
                        Label = "Other Operational Exception",
                        Description = "Circumstance not captured above.",
                        RequiresFollowUpNote = true,
                        FollowUpPlaceholder = "Provide detailed explanation (minimum 10 characters)",
                        RequiresPhotoProof = false
                    }
                }
            };

            return Task.FromResult(schema);
        }
    }
}

