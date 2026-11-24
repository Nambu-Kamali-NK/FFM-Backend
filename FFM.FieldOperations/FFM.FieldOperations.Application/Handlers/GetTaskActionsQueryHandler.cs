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
    public class GetTaskActionsQueryHandler : IRequestHandler<GetTaskActionsQuery, TaskActionsDto>
    {
        private readonly ITaskRepository _taskRepository;

        public GetTaskActionsQueryHandler(ITaskRepository taskRepository)
        {
            _taskRepository = taskRepository;
        }

        public async Task<TaskActionsDto> Handle(GetTaskActionsQuery request, CancellationToken cancellationToken)
        {
            if (request.TaskId <= 0)
            {
                throw new ArgumentException("Valid TaskId is required.", nameof(request.TaskId));
            }

            var task = await _taskRepository.GetByIdAsync(request.TaskId, cancellationToken);
            if (task == null)
            {
                throw new KeyNotFoundException($"Task with ID {request.TaskId} was not found.");
            }

            var result = new TaskActionsDto
            {
                TaskId = task.Id,
                TaskNumber = task.TaskNumber,
                CurrentStatus = task.Status,
                TaskType = task.TaskType
            };

            switch (task.Status)
            {
                case "CREATED":
                    result.Actions.Add(new TaskActionItemDto
                    {
                        ActionKey = "ASSIGN_TECHNICIAN",
                        Label = "Assign Technician",
                        Style = "PRIMARY",
                        Icon = "person_add",
                        TargetEndpoint = "/api/v1/Tasks/reassign",
                        HttpMethod = "POST",
                        Payload = new Dictionary<string, object> { ["taskId"] = task.Id }
                    });
                    result.Actions.Add(new TaskActionItemDto
                    {
                        ActionKey = "CANCEL_TASK",
                        Label = "Cancel Task",
                        Style = "DANGER",
                        Icon = "cancel",
                        TargetEndpoint = "/api/v1/Tasks/cancel",
                        HttpMethod = "POST",
                        RequiresConfirmation = true,
                        ConfirmationTitle = "Cancel Task?",
                        ConfirmationMessage = "Are you sure you want to cancel this unassigned task?",
                        Payload = new Dictionary<string, object> { ["taskId"] = task.Id }
                    });
                    break;

                case "ASSIGNED":
                    result.Actions.Add(new TaskActionItemDto
                    {
                        ActionKey = "START_TRAVEL",
                        Label = "Start Travel",
                        Style = "PRIMARY",
                        Icon = "navigation",
                        TargetEndpoint = "/api/v1/Tasks/status",
                        HttpMethod = "POST",
                        RequiresConfirmation = true,
                        ConfirmationTitle = "Start Departure?",
                        ConfirmationMessage = "Notify dispatch and client that technician is en-route.",
                        Payload = new Dictionary<string, object>
                        {
                            ["taskId"] = task.Id,
                            ["status"] = "EN_ROUTE"
                        }
                    });
                    result.Actions.Add(new TaskActionItemDto
                    {
                        ActionKey = "REASSIGN_TASK",
                        Label = "Reassign Technician",
                        Style = "SECONDARY",
                        Icon = "swap_horiz",
                        TargetEndpoint = "/api/v1/Tasks/reassign",
                        HttpMethod = "POST",
                        Payload = new Dictionary<string, object> { ["taskId"] = task.Id }
                    });
                    result.Actions.Add(new TaskActionItemDto
                    {
                        ActionKey = "CANCEL_TASK",
                        Label = "Cancel Task",
                        Style = "DANGER",
                        Icon = "cancel",
                        TargetEndpoint = "/api/v1/Tasks/cancel",
                        HttpMethod = "POST",
                        RequiresConfirmation = true,
                        ConfirmationTitle = "Cancel Work Order?",
                        ConfirmationMessage = "Technician assignment will be released.",
                        Payload = new Dictionary<string, object> { ["taskId"] = task.Id }
                    });
                    break;

                case "EN_ROUTE":
                    result.Actions.Add(new TaskActionItemDto
                    {
                        ActionKey = "SITE_CHECK_IN",
                        Label = "GPS Site Check-in",
                        Style = "SUCCESS",
                        Icon = "location_on",
                        TargetEndpoint = "/api/v1/Visits/site-check-in",
                        HttpMethod = "POST",
                        Payload = new Dictionary<string, object> { ["taskId"] = task.Id }
                    });
                    result.Actions.Add(new TaskActionItemDto
                    {
                        ActionKey = "CANCEL_TASK",
                        Label = "Abort / Cancel Travel",
                        Style = "DANGER",
                        Icon = "cancel",
                        TargetEndpoint = "/api/v1/Tasks/cancel",
                        HttpMethod = "POST",
                        RequiresConfirmation = true,
                        ConfirmationTitle = "Abort Assignment?",
                        ConfirmationMessage = "Provide mandatory reason for cancelling en-route work order.",
                        Payload = new Dictionary<string, object> { ["taskId"] = task.Id }
                    });
                    break;

                case "CHECKED_IN":
                    result.Actions.Add(new TaskActionItemDto
                    {
                        ActionKey = "RECORD_EVIDENCE",
                        Label = "Collect Proof of Work",
                        Style = "PRIMARY",
                        Icon = "camera_alt",
                        TargetEndpoint = "/api/v1/Visits/evidence",
                        HttpMethod = "POST"
                    });
                    result.Actions.Add(new TaskActionItemDto
                    {
                        ActionKey = "SITE_CHECK_OUT",
                        Label = "Complete & Check Out",
                        Style = "SUCCESS",
                        Icon = "check_circle",
                        TargetEndpoint = "/api/v1/Visits/site-check-out",
                        HttpMethod = "POST",
                        RequiresConfirmation = true,
                        ConfirmationTitle = "Finalize Visit?",
                        ConfirmationMessage = "Ensure all required equipment checklist items have been uploaded before closing."
                    });
                    break;

                case "COMPLETED":
                    result.Actions.Add(new TaskActionItemDto
                    {
                        ActionKey = "VIEW_EVIDENCE",
                        Label = "View Recorded Evidences",
                        Style = "SECONDARY",
                        Icon = "visibility",
                        TargetEndpoint = $"/api/v1/Visits/evidence?taskId={task.Id}",
                        HttpMethod = "GET"
                    });
                    break;

                case "CANCELLED":
                    result.Actions.Add(new TaskActionItemDto
                    {
                        ActionKey = "VIEW_DETAILS",
                        Label = "View Cancellation Reason",
                        Style = "SECONDARY",
                        Icon = "info",
                        TargetEndpoint = $"/api/v1/Tasks",
                        HttpMethod = "GET"
                    });
                    break;
            }

            return result;
        }
    }
}

