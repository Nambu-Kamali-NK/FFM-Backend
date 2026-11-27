using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using FFM.FieldOperations.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace FFM.FieldOperations.Application.Services
{
    public class TaskNotificationDispatcher : ITaskNotificationDispatcher
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<TaskNotificationDispatcher> _logger;

        public TaskNotificationDispatcher(HttpClient httpClient, ILogger<TaskNotificationDispatcher> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task DispatchTaskAssignmentNotificationAsync(
            int taskId,
            string taskNumber,
            string taskTitle,
            int assignedEmployeeId,
            string clientSiteName,
            string priority,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var payload = new
                {
                    recipientEmployeeId = assignedEmployeeId,
                    templateCode = "TMPL-002",
                    channel = "PUSH",
                    priority = string.IsNullOrWhiteSpace(priority) ? "HIGH" : priority.ToUpperInvariant(),
                    placeholders = new Dictionary<string, string>
                    {
                        { "TaskId", taskNumber },
                        { "SiteName", clientSiteName },
                        { "TaskTitle", taskTitle }
                    }
                };

                _logger.LogInformation(
                    "[NotificationDispatcher] Dispatching Task Assignment push notification for Task {TaskNumber} to Employee {EmployeeId}",
                    taskNumber, assignedEmployeeId);

                var response = await _httpClient.PostAsJsonAsync("api/v1/Notifications/send", payload, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation(
                        "[NotificationDispatcher] Successfully dispatched push notification for Task {TaskNumber}", taskNumber);
                }
                else
                {
                    _logger.LogWarning(
                        "[NotificationDispatcher] Notification service returned non-success code {StatusCode} for Task {TaskNumber}",
                        response.StatusCode, taskNumber);
                }
            }
            catch (Exception ex)
            {
                // Resilient: Never fail task assignment transaction due to notification transport issues
                _logger.LogWarning(ex, "[NotificationDispatcher] Failed to dispatch push notification for Task {TaskNumber}", taskNumber);
            }
        }
    }
}
