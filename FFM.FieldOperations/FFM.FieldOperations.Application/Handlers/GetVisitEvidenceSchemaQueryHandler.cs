using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFM.FieldOperations.Application.DTOs;
using FFM.FieldOperations.Application.Queries;
using FFM.FieldOperations.Domain.Interfaces;
using MediatR;

namespace FFM.FieldOperations.Application.Handlers
{
    public class GetVisitEvidenceSchemaQueryHandler : IRequestHandler<GetVisitEvidenceSchemaQuery, TaskEvidenceSchemaDto>
    {
        private readonly ITaskRepository _taskRepository;

        public GetVisitEvidenceSchemaQueryHandler(ITaskRepository taskRepository)
        {
            _taskRepository = taskRepository;
        }

        public async Task<TaskEvidenceSchemaDto> Handle(GetVisitEvidenceSchemaQuery request, CancellationToken cancellationToken)
        {
            var taskType = request?.TaskType;

            // If TaskId is provided, resolve the task category directly from the database
            if (request?.TaskId.HasValue == true && request.TaskId.Value > 0)
            {
                var task = await _taskRepository.GetByIdAsync(request.TaskId.Value, cancellationToken);
                if (task != null && !string.IsNullOrWhiteSpace(task.TaskType))
                {
                    taskType = task.TaskType;
                }
            }

            var normalizedType = string.IsNullOrWhiteSpace(taskType) ? "GENERAL" : taskType.Trim().ToUpperInvariant();

            var schema = new TaskEvidenceSchemaDto
            {
                TaskType = normalizedType
            };

            switch (normalizedType)
            {
                case "AC_MAINTENANCE":
                    schema.Title = "HVAC & AC Maintenance Evidence Checklist";
                    schema.Description = "Capture pressure readings, coil condition photo, refrigerant checks, and customer sign-off.";
                    schema.Items = new List<EvidenceSchemaItemDto>
                    {
                        new EvidenceSchemaItemDto
                        {
                            Key = "compressorPressurePsi",
                            Label = "Compressor Suction Pressure",
                            EvidenceType = "METER_READING",
                            Widget = "NUMBER_INPUT",
                            Required = true,
                            Unit = "PSI",
                            Placeholder = "e.g., 65 - 75"
                        },
                        new EvidenceSchemaItemDto
                        {
                            Key = "coolingTemperatureC",
                            Label = "Vent Air Temperature",
                            EvidenceType = "METER_READING",
                            Widget = "NUMBER_INPUT",
                            Required = true,
                            Unit = "°C",
                            Placeholder = "e.g., 18.5"
                        },
                        new EvidenceSchemaItemDto
                        {
                            Key = "coilCleaningPhoto",
                            Label = "Evaporator / Condenser Coil Photo",
                            EvidenceType = "PHOTO",
                            Widget = "CAMERA_CAPTURE",
                            Required = true,
                            Description = "Clear photo showing cleaned condenser fins"
                        },
                        new EvidenceSchemaItemDto
                        {
                            Key = "refrigerantLeakCheckPassed",
                            Label = "Refrigerant Sniffer Leak Test Passed",
                            EvidenceType = "CHECKLIST",
                            Widget = "SWITCH",
                            Required = true,
                            Description = "Confirm electronic detector showed 0 PPM"
                        },
                        new EvidenceSchemaItemDto
                        {
                            Key = "customerSignature",
                            Label = "Client Acknowledgement Signature",
                            EvidenceType = "SIGNATURE",
                            Widget = "SIGNATURE_PAD",
                            Required = true,
                            Description = "On-screen signature of client facility representative"
                        }
                    };
                    break;

                case "SOLAR_PANEL":
                    schema.Title = "Solar PV Array Servicing Evidence Checklist";
                    schema.Description = "Submit inverter generation metrics, array cleaning photos, and net metering confirmation.";
                    schema.Items = new List<EvidenceSchemaItemDto>
                    {
                        new EvidenceSchemaItemDto
                        {
                            Key = "inverterGenerationKw",
                            Label = "Inverter Real-Time Output",
                            EvidenceType = "METER_READING",
                            Widget = "NUMBER_INPUT",
                            Required = true,
                            Unit = "kW",
                            Placeholder = "e.g., 12.4"
                        },
                        new EvidenceSchemaItemDto
                        {
                            Key = "arrayCleanlinessPhoto",
                            Label = "Rooftop PV Array Post-Cleaning Photo",
                            EvidenceType = "PHOTO",
                            Widget = "CAMERA_CAPTURE",
                            Required = true,
                            Description = "Panoramic photo of cleaned solar panels"
                        },
                        new EvidenceSchemaItemDto
                        {
                            Key = "batteryStorageVoltage",
                            Label = "Battery Bank Terminal Voltage",
                            EvidenceType = "METER_READING",
                            Widget = "NUMBER_INPUT",
                            Required = false,
                            Unit = "V",
                            Placeholder = "e.g., 48.2"
                        },
                        new EvidenceSchemaItemDto
                        {
                            Key = "customerSignature",
                            Label = "Customer Sign-off Signature",
                            EvidenceType = "SIGNATURE",
                            Widget = "SIGNATURE_PAD",
                            Required = true,
                            Description = "Facility supervisor authorization signature"
                        }
                    };
                    break;

                case "ELEVATOR":
                    schema.Title = "Elevator & Lift Maintenance Evidence Checklist";
                    schema.Description = "Document mechanical brake safety checks, cable inspection, and building sign-off.";
                    schema.Items = new List<EvidenceSchemaItemDto>
                    {
                        new EvidenceSchemaItemDto
                        {
                            Key = "brakeTestPhoto",
                            Label = "Emergency Brake Mechanism Inspection Photo",
                            EvidenceType = "PHOTO",
                            Widget = "CAMERA_CAPTURE",
                            Required = true,
                            Description = "Close-up photo of brake shoe and governor caliper"
                        },
                        new EvidenceSchemaItemDto
                        {
                            Key = "cableTensionCondition",
                            Label = "Hoist Cable Tension Assessment",
                            EvidenceType = "CHECKLIST",
                            Widget = "DROPDOWN",
                            Required = true,
                            Options = new List<string> { "OPTIMAL", "ACCEPTABLE", "REQUIRES_ADJUSTMENT", "CRITICAL_REPLACE" }
                        },
                        new EvidenceSchemaItemDto
                        {
                            Key = "safetySensorTestPassed",
                            Label = "Door Infrared Sensor & Safety Edge Test Passed",
                            EvidenceType = "CHECKLIST",
                            Widget = "SWITCH",
                            Required = true,
                            Description = "Confirm automatic door reopening sensor activates properly"
                        },
                        new EvidenceSchemaItemDto
                        {
                            Key = "buildingManagerSignature",
                            Label = "Building / Property Manager Signature",
                            EvidenceType = "SIGNATURE",
                            Widget = "SIGNATURE_PAD",
                            Required = true,
                            Description = "Digital signature of licensed building authority"
                        }
                    };
                    break;

                default: // GENERAL
                    schema.Title = "General On-Site Service Evidence";
                    schema.Description = "Standard work completion documentation: photo proof and client signature.";
                    schema.Items = new List<EvidenceSchemaItemDto>
                    {
                        new EvidenceSchemaItemDto
                        {
                            Key = "workProofPhoto",
                            Label = "Completed Work Photo",
                            EvidenceType = "PHOTO",
                            Widget = "CAMERA_CAPTURE",
                            Required = true,
                            Description = "Photo showing the completed servicing or repair"
                        },
                        new EvidenceSchemaItemDto
                        {
                            Key = "clientSignature",
                            Label = "Customer / Client Signature",
                            EvidenceType = "SIGNATURE",
                            Widget = "SIGNATURE_PAD",
                            Required = true,
                            Description = "Client sign-off confirming job completion"
                        }
                    };
                    break;
            }

            return schema;
        }
    }
}
