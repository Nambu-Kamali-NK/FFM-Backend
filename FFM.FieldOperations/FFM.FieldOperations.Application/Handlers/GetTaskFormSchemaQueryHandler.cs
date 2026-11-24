using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FFM.FieldOperations.Application.DTOs;
using FFM.FieldOperations.Application.Queries;
using MediatR;

namespace FFM.FieldOperations.Application.Handlers
{
    public class GetTaskFormSchemaQueryHandler : IRequestHandler<GetTaskFormSchemaQuery, TaskFormSchemaDto>
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public Task<TaskFormSchemaDto> Handle(GetTaskFormSchemaQuery request, CancellationToken cancellationToken)
        {
            var taskType = string.IsNullOrWhiteSpace(request?.TaskType) ? "GENERAL" : request.TaskType.Trim().ToUpperInvariant();

            // 1. Attempt to load dynamically from the physical JSON file: schemas/task_form_schemas.json
            var schemaFromJson = TryLoadSchemaFromJsonFile(taskType);
            if (schemaFromJson != null)
            {
                return Task.FromResult(schemaFromJson);
            }

            // 2. Programmatic Fallback if physical JSON file is absent
            var schema = new TaskFormSchemaDto
            {
                TaskType = taskType
            };

            var baseSection = new TaskFormSectionDto
            {
                Title = "General Job Details",
                Description = "Basic work order parameters and scheduling",
                Fields = new List<TaskFormFieldDto>
                {
                    new TaskFormFieldDto
                    {
                        Key = "title",
                        Label = "Task Title",
                        Widget = "TEXT_INPUT",
                        Required = true,
                        Placeholder = "e.g., Annual Chiller Maintenance"
                    },
                    new TaskFormFieldDto
                    {
                        Key = "clientSiteId",
                        Label = "Client Site Location",
                        Widget = "SEARCHABLE_DROPDOWN",
                        Required = true,
                        Placeholder = "Select client facility"
                    },
                    new TaskFormFieldDto
                    {
                        Key = "priority",
                        Label = "Task Priority",
                        Widget = "DROPDOWN",
                        Required = true,
                        DefaultValue = "MEDIUM",
                        Options = new List<string> { "LOW", "MEDIUM", "HIGH", "URGENT" }
                    },
                    new TaskFormFieldDto
                    {
                        Key = "scheduledStart",
                        Label = "Scheduled Start Time",
                        Widget = "DATE_TIME_PICKER",
                        Required = true
                    },
                    new TaskFormFieldDto
                    {
                        Key = "scheduledEnd",
                        Label = "Scheduled End Time",
                        Widget = "DATE_TIME_PICKER",
                        Required = true
                    },
                    new TaskFormFieldDto
                    {
                        Key = "assignedEmployeeId",
                        Label = "Assign Field Technician",
                        Widget = "DROPDOWN",
                        Required = false,
                        Placeholder = "Select technician (optional)"
                    },
                    new TaskFormFieldDto
                    {
                        Key = "description",
                        Label = "Work Scope & Instructions",
                        Widget = "TEXT_AREA",
                        Required = false,
                        Placeholder = "Enter detailed scope of work..."
                    }
                }
            };

            schema.Sections.Add(baseSection);

            switch (taskType)
            {
                case "AC_MAINTENANCE":
                    schema.FormTitle = "Create AC Maintenance Task";
                    schema.Description = "Dynamic work order form for Heating, Ventilation, and Air Conditioning servicing.";
                    schema.Sections.Add(new TaskFormSectionDto
                    {
                        Title = "AC Equipment Specifications",
                        Description = "Technical parameters captured dynamically",
                        Fields = new List<TaskFormFieldDto>
                        {
                            new TaskFormFieldDto
                            {
                                Key = "acBrand",
                                Label = "AC Manufacturer / Brand",
                                Widget = "DROPDOWN",
                                Required = true,
                                Options = new List<string> { "Daikin", "Voltas", "Blue Star", "Carrier", "Hitachi", "LG", "Other" }
                            },
                            new TaskFormFieldDto
                            {
                                Key = "acTonnage",
                                Label = "Cooling Capacity (Tonnage)",
                                Widget = "NUMBER_INPUT",
                                Required = true,
                                Placeholder = "e.g., 2.0"
                            },
                            new TaskFormFieldDto
                            {
                                Key = "refrigerantType",
                                Label = "Refrigerant Gas Type",
                                Widget = "DROPDOWN",
                                Required = true,
                                Options = new List<string> { "R-410A", "R-32", "R-22", "R-134a", "Other" }
                            },
                            new TaskFormFieldDto
                            {
                                Key = "floorNumber",
                                Label = "Floor / Unit Number",
                                Widget = "NUMBER_INPUT",
                                Required = true,
                                Placeholder = "e.g., 4"
                            },
                            new TaskFormFieldDto
                            {
                                Key = "isUnderWarranty",
                                Label = "Equipment Under Manufacturer Warranty?",
                                Widget = "CHECKBOX",
                                Required = false,
                                DefaultValue = false
                            }
                        }
                    });
                    break;

                case "SOLAR_PANEL":
                    schema.FormTitle = "Create Solar PV Servicing Task";
                    schema.Description = "Dynamic work order form for Solar Rooftop and Photovoltaic servicing.";
                    schema.Sections.Add(new TaskFormSectionDto
                    {
                        Title = "Solar Array Technical Specifications",
                        Description = "Photovoltaic parameters captured dynamically",
                        Fields = new List<TaskFormFieldDto>
                        {
                            new TaskFormFieldDto
                            {
                                Key = "capacityKw",
                                Label = "Plant Capacity (kW)",
                                Widget = "NUMBER_INPUT",
                                Required = true,
                                Placeholder = "e.g., 50"
                            },
                            new TaskFormFieldDto
                            {
                                Key = "panelCount",
                                Label = "Total PV Panel Count",
                                Widget = "NUMBER_INPUT",
                                Required = true,
                                Placeholder = "e.g., 120"
                            },
                            new TaskFormFieldDto
                            {
                                Key = "inverterBrand",
                                Label = "Inverter Make / Model",
                                Widget = "TEXT_INPUT",
                                Required = true,
                                Placeholder = "e.g., Sungrow / ABB"
                            },
                            new TaskFormFieldDto
                            {
                                Key = "cleaningRequired",
                                Label = "Panel Cleaning & Descaling Required?",
                                Widget = "CHECKBOX",
                                Required = false,
                                DefaultValue = true
                            }
                        }
                    });
                    break;

                case "ELEVATOR":
                    schema.FormTitle = "Create Elevator Maintenance Task";
                    schema.Description = "Dynamic work order form for Elevator and Lift inspection.";
                    schema.Sections.Add(new TaskFormSectionDto
                    {
                        Title = "Elevator Engineering Specifications",
                        Description = "Safety parameters captured dynamically",
                        Fields = new List<TaskFormFieldDto>
                        {
                            new TaskFormFieldDto
                            {
                                Key = "elevatorBrand",
                                Label = "Elevator Manufacturer",
                                Widget = "DROPDOWN",
                                Required = true,
                                Options = new List<string> { "Otis", "Schindler", "Kone", "Thyssenkrupp", "Mitsubishi", "Other" }
                            },
                            new TaskFormFieldDto
                            {
                                Key = "liftNumber",
                                Label = "Lift Identifier / Shaft ID",
                                Widget = "TEXT_INPUT",
                                Required = true,
                                Placeholder = "e.g., Passenger Lift A"
                            },
                            new TaskFormFieldDto
                            {
                                Key = "floorsServiced",
                                Label = "Total Floors Serviced",
                                Widget = "NUMBER_INPUT",
                                Required = true,
                                Placeholder = "e.g., 15"
                            },
                            new TaskFormFieldDto
                            {
                                Key = "loadCapacityKg",
                                Label = "Max Weight Capacity (kg)",
                                Widget = "NUMBER_INPUT",
                                Required = true,
                                Placeholder = "e.g., 1000"
                            }
                        }
                    });
                    break;

                default:
                    schema.FormTitle = "Create General Task";
                    schema.Description = "Standard work order form for general facility maintenance.";
                    break;
            }

            return Task.FromResult(schema);
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> FormSchemaRawJsonCache = new(StringComparer.OrdinalIgnoreCase);
        private static volatile bool _formSchemasLoaded = false;
        private static readonly object _formSchemaLock = new();

        private static void EnsureFormSchemasLoaded()
        {
            if (_formSchemasLoaded) return;

            lock (_formSchemaLock)
            {
                if (_formSchemasLoaded) return;

                try
                {
                    var candidatePaths = new[]
                    {
                        Path.Combine(AppContext.BaseDirectory, "schemas", "task_form_schemas.json"),
                        Path.Combine(Directory.GetCurrentDirectory(), "schemas", "task_form_schemas.json"),
                        Path.Combine(Directory.GetCurrentDirectory(), "FFM.FieldOperations", "FFM.FieldOperations.Api", "schemas", "task_form_schemas.json")
                    };

                    foreach (var path in candidatePaths)
                    {
                        if (File.Exists(path))
                        {
                            var jsonContent = File.ReadAllText(path);
                            using var doc = JsonDocument.Parse(jsonContent);
                            foreach (var prop in doc.RootElement.EnumerateObject())
                            {
                                FormSchemaRawJsonCache[prop.Name] = prop.Value.GetRawText();
                            }
                            _formSchemasLoaded = true;
                            break;
                        }
                    }
                }
                catch
                {
                    // Fallback
                }
            }
        }

        private static TaskFormSchemaDto? TryLoadSchemaFromJsonFile(string taskType)
        {
            EnsureFormSchemasLoaded();

            if (FormSchemaRawJsonCache.TryGetValue(taskType, out var rawJson))
            {
                return JsonSerializer.Deserialize<TaskFormSchemaDto>(rawJson, JsonOptions);
            }

            return null;
        }
    }
}
