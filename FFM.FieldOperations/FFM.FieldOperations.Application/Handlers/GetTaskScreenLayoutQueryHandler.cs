using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FFM.FieldOperations.Application.DTOs;
using FFM.FieldOperations.Application.Queries;
using FFM.FieldOperations.Domain.Entities;
using FFM.FieldOperations.Domain.Interfaces;
using FFM.FieldOperations.Application.Interfaces;
using MediatR;

namespace FFM.FieldOperations.Application.Handlers
{
    public class GetTaskScreenLayoutQueryHandler : IRequestHandler<GetTaskScreenLayoutQuery, ScreenLayoutDto>
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly ITaskRepository? _taskRepository;
        private readonly IClientSiteRepository? _clientSiteRepository;
        private readonly IMediator? _mediator;

        public GetTaskScreenLayoutQueryHandler()
        {
        }

        public GetTaskScreenLayoutQueryHandler(
            ITaskRepository taskRepository,
            IClientSiteRepository clientSiteRepository,
            IMediator mediator)
        {
            _taskRepository = taskRepository;
            _clientSiteRepository = clientSiteRepository;
            _mediator = mediator;
        }

        public async Task<ScreenLayoutDto> Handle(GetTaskScreenLayoutQuery request, CancellationToken cancellationToken)
        {
            TaskItem? task = null;
            ClientSite? site = null;
            string taskType = "GENERAL";

            if (request.TaskId.HasValue && request.TaskId.Value > 0 && _taskRepository != null)
            {
                task = await _taskRepository.GetByIdAsync(request.TaskId.Value, cancellationToken);
                if (task == null)
                {
                    throw new KeyNotFoundException($"Task with ID {request.TaskId.Value} was not found.");
                }

                taskType = string.IsNullOrWhiteSpace(task.TaskType) ? "GENERAL" : task.TaskType.Trim().ToUpperInvariant();

                site = task.ClientSite;
                if (site == null && _clientSiteRepository != null)
                {
                    site = await _clientSiteRepository.GetByIdAsync(task.ClientSiteId);
                }
            }
            else if (!string.IsNullOrWhiteSpace(request.TaskType))
            {
                taskType = request.TaskType.Trim().ToUpperInvariant();
            }

            // 1. Load base screen layout template from schemas/task_screen_templates.json
            var layout = TryLoadScreenTemplate(taskType) ?? CreateFallbackScreenLayout(taskType);

            // 2. Hydrate with live task and site details if available
            if (task != null)
            {
                layout.TaskId = task.Id;
                layout.TaskNumber = task.TaskNumber;
                layout.CurrentStatus = task.Status;
                layout.Title = $"{layout.Title} - {task.TaskNumber}";

                foreach (var widget in layout.Widgets)
                {
                    if (widget.WidgetType == "HERO_HEADER")
                    {
                        widget.Properties["taskId"] = task.Id;
                        widget.Properties["taskNumber"] = task.TaskNumber;
                        widget.Properties["title"] = task.Title;
                        widget.Properties["description"] = task.Description ?? string.Empty;
                        widget.Properties["status"] = task.Status;
                        widget.Properties["statusColor"] = task.Status == "COMPLETED" ? "#059669" : (task.Status == "EN_ROUTE" ? "#D97706" : "#2563EB");
                        widget.Properties["priority"] = task.Priority;
                        widget.Properties["priorityColor"] = task.Priority == "URGENT" || task.Priority == "HIGH" ? "#DC2626" : "#2563EB";
                        widget.Properties["scheduledWindow"] = $"{task.ScheduledStart:yyyy-MM-dd HH:mm} - {task.ScheduledEnd:HH:mm}";
                    }
                    else if (widget.WidgetType == "CLIENT_SITE_CARD" && site != null)
                    {
                        widget.Properties["clientSiteId"] = site.Id;
                        widget.Properties["siteCode"] = site.SiteCode;
                        widget.Properties["siteName"] = site.Name;
                        widget.Properties["address"] = $"{site.Address}, {site.City}, {site.State} {site.PostalCode}";
                        widget.Properties["contactPerson"] = site.ContactPerson ?? string.Empty;
                        widget.Properties["contactPhone"] = site.ContactPhone ?? string.Empty;
                        widget.Properties["latitude"] = site.Latitude;
                        widget.Properties["longitude"] = site.Longitude;
                        widget.Properties["radiusMeters"] = site.RadiusMeters;
                        widget.Properties["mapNavigationUrl"] = $"https://maps.google.com/?q={site.Latitude},{site.Longitude}";
                    }
                    else if (widget.WidgetType == "BOTTOM_ACTION_BAR" && _mediator != null)
                    {
                        try
                        {
                            var actionsResult = await _mediator.Send(new GetTaskActionsQuery(task.Id), cancellationToken);
                            widget.Properties["actions"] = actionsResult.Actions;
                        }
                        catch
                        {
                            // Keep default template actions if query fails
                        }
                    }
                }
            }

            return layout;
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> ScreenTemplateRawJsonCache = new(StringComparer.OrdinalIgnoreCase);
        private static volatile bool _templatesLoaded = false;
        private static readonly object _loadLock = new();

        private static void EnsureTemplatesLoaded()
        {
            if (_templatesLoaded) return;

            lock (_loadLock)
            {
                if (_templatesLoaded) return;

                try
                {
                    var candidatePaths = new[]
                    {
                        Path.Combine(AppContext.BaseDirectory, "schemas", "task_screen_templates.json"),
                        Path.Combine(Directory.GetCurrentDirectory(), "schemas", "task_screen_templates.json"),
                        Path.Combine(Directory.GetCurrentDirectory(), "FFM.FieldOperations", "FFM.FieldOperations.Api", "schemas", "task_screen_templates.json")
                    };

                    foreach (var path in candidatePaths)
                    {
                        if (File.Exists(path))
                        {
                            var jsonContent = File.ReadAllText(path);
                            using var doc = JsonDocument.Parse(jsonContent);
                            foreach (var prop in doc.RootElement.EnumerateObject())
                            {
                                ScreenTemplateRawJsonCache[prop.Name] = prop.Value.GetRawText();
                            }
                            _templatesLoaded = true;
                            break;
                        }
                    }
                }
                catch
                {
                    // Fallback to runtime creation
                }
            }
        }

        private static ScreenLayoutDto? TryLoadScreenTemplate(string taskType)
        {
            EnsureTemplatesLoaded();

            if (ScreenTemplateRawJsonCache.TryGetValue(taskType, out var rawJson))
            {
                return JsonSerializer.Deserialize<ScreenLayoutDto>(rawJson, JsonOptions);
            }

            return null;
        }

        private static ScreenLayoutDto CreateFallbackScreenLayout(string taskType)
        {
            return new ScreenLayoutDto
            {
                ScreenId = "TASK_EXECUTION_SCREEN",
                Title = $"{taskType} Work Order Execution",
                Version = "2.0",
                TaskType = taskType,
                Theme = new ScreenThemeDto
                {
                    PrimaryColor = "#1E3A8A",
                    AccentColor = "#2563EB",
                    BackgroundColor = "#F8FAFC",
                    CardBackgroundColor = "#FFFFFF"
                },
                Widgets = new List<ScreenWidgetDto>
                {
                    new ScreenWidgetDto
                    {
                        WidgetId = "hero_header",
                        WidgetType = "HERO_HEADER",
                        Title = "Work Order Overview",
                        Order = 1,
                        IsVisible = true,
                        Properties = new Dictionary<string, object>
                        {
                            { "showCountdownTimer", true },
                            { "badgeStyle", "PILL" }
                        }
                    },
                    new ScreenWidgetDto
                    {
                        WidgetId = "client_site_info",
                        WidgetType = "CLIENT_SITE_CARD",
                        Title = "Client Facility & Geofence",
                        Order = 2,
                        IsVisible = true,
                        Properties = new Dictionary<string, object>
                        {
                            { "enableDirectCall", true },
                            { "enableMapNavigation", true },
                            { "showGeofenceBoundaryPill", true }
                        }
                    },
                    new ScreenWidgetDto
                    {
                        WidgetId = "form_specifications",
                        WidgetType = "FORM_SECTION",
                        Title = "Job Specifications & Details",
                        Order = 3,
                        IsVisible = true,
                        Properties = new Dictionary<string, object>
                        {
                            { "category", taskType }
                        }
                    },
                    new ScreenWidgetDto
                    {
                        WidgetId = "telemetry_readings",
                        WidgetType = "TELEMETRY_GAUGES",
                        Title = "Operational Telemetry Readings",
                        Order = 4,
                        IsVisible = true,
                        Properties = new Dictionary<string, object>()
                    },
                    new ScreenWidgetDto
                    {
                        WidgetId = "evidence_gallery",
                        WidgetType = "EVIDENCE_GALLERY",
                        Title = "Proof of Work (Photos & Signatures)",
                        Order = 5,
                        IsVisible = true,
                        Properties = new Dictionary<string, object>
                        {
                            { "allowMultiPhotoUpload", true }
                        }
                    },
                    new ScreenWidgetDto
                    {
                        WidgetId = "bottom_action_bar",
                        WidgetType = "BOTTOM_ACTION_BAR",
                        Title = "Execution State Machine Actions",
                        Order = 6,
                        IsVisible = true,
                        Properties = new Dictionary<string, object>
                        {
                            { "isSticky", true }
                        }
                    }
                }
            };
        }
    }
}

