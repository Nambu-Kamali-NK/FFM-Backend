using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using FFM.GPS.Tracking.Application.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace FFM.GPS.Tracking.Api.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception ex)
        {
            context.Response.ContentType = "application/json";

            switch (ex)
            {
                case ArgumentException argEx:
                {
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    var response = ApiResponse<object>.Fail("VALIDATION_ERROR", argEx.Message, "Validation failed");
                    var json = JsonSerializer.Serialize(response);
                    await context.Response.WriteAsync(json);
                    break;
                }

                case UnauthorizedAccessException authEx:
                {
                    _logger.LogWarning(authEx, "Authorization failure: {Message}", authEx.Message);
                    context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    var response = ApiResponse<object>.Fail("FORBIDDEN", authEx.Message, "You do not have permission to perform this action");
                    var json = JsonSerializer.Serialize(response);
                    await context.Response.WriteAsync(json);
                    break;
                }

                case InvalidOperationException invEx:
                {
                    _logger.LogWarning(invEx, "Business operation conflict: {Message}", invEx.Message);
                    context.Response.StatusCode = (int)HttpStatusCode.Conflict;
                    var response = ApiResponse<object>.Fail("CONFLICT", invEx.Message, "Resource conflict");
                    var json = JsonSerializer.Serialize(response);
                    await context.Response.WriteAsync(json);
                    break;
                }

                case System.Collections.Generic.KeyNotFoundException notFoundEx:
                {
                    _logger.LogWarning(notFoundEx, "Resource not found: {Message}", notFoundEx.Message);
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    var response = ApiResponse<object>.Fail("NOT_FOUND", notFoundEx.Message, "Resource not found");
                    var json = JsonSerializer.Serialize(response);
                    await context.Response.WriteAsync(json);
                    break;
                }

                default:
                {
                    _logger.LogError(ex, "Unhandled server error occurred: {Message}", ex.Message);
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    var response = ApiResponse<object>.Fail("INTERNAL_SERVER_ERROR", "An unexpected error occurred while processing your request.", "Server error");
                    var json = JsonSerializer.Serialize(response);
                    await context.Response.WriteAsync(json);
                    break;
                }
            }
        }
    }
}
