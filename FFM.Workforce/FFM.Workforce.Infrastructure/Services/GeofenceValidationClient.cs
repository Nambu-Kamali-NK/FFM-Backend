using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FFM.Workforce.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FFM.Workforce.Infrastructure.Services
{
    public class GeofenceValidationClient : IGeofenceValidationClient
    {
        private readonly HttpClient _httpClient;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<GeofenceValidationClient> _logger;

        public GeofenceValidationClient(
            HttpClient httpClient,
            IConfiguration configuration,
            IHttpContextAccessor httpContextAccessor,
            ILogger<GeofenceValidationClient> logger)
        {
            _httpClient = httpClient;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;

            var baseUrl = configuration["GpsService:BaseUrl"]
                ?? Environment.GetEnvironmentVariable("GpsService__BaseUrl")
                ?? "http://gps-api";

            _httpClient.BaseAddress = new Uri(baseUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(5);
        }

        public async Task<GeofenceValidationResult> ValidateLocationAsync(int employeeId, double latitude, double longitude, double gpsAccuracyMeters = 10.0)
        {
            try
            {
                var payload = new
                {
                    EmployeeId = employeeId,
                    Latitude = latitude,
                    Longitude = longitude,
                    GpsAccuracyMeters = gpsAccuracyMeters
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/Geofences/validate")
                {
                    Content = JsonContent.Create(payload)
                };

                // Forward bearer token if present
                var authHeader = _httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString();
                if (!string.IsNullOrWhiteSpace(authHeader))
                {
                    request.Headers.TryAddWithoutValidation("Authorization", authHeader);
                }

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var jsonStr = await response.Content.ReadAsStringAsync();
                    try
                    {
                        var envelope = JsonSerializer.Deserialize<FFM.Workforce.Application.DTOs.ApiResponse<GeofenceValidationResult>>(jsonStr, options);
                        if (envelope?.Data != null)
                        {
                            return envelope.Data;
                        }
                    }
                    catch
                    {
                        // Fallback to direct serialization
                    }

                    var result = JsonSerializer.Deserialize<GeofenceValidationResult>(jsonStr, options);
                    return result ?? new GeofenceValidationResult { IsValidGeofence = false, Message = "Empty response from GPS validation service." };
                }

                _logger.LogWarning("GPS Validation service returned status {StatusCode}", response.StatusCode);
                return new GeofenceValidationResult
                {
                    IsValidGeofence = false,
                    Message = "Unable to verify geofence location boundary."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to GPS validation service: {Message}", ex.Message);
                throw new HttpRequestException("GPS Geofence validation service is offline or unreachable.", ex);
            }
        }
    }
}
