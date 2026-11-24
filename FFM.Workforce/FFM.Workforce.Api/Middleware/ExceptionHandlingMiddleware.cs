using System;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using FluentValidation;
using FFM.Workforce.Application.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace FFM.Workforce.Api.Middleware
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
                case ValidationException valEx:
                {
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    var errors = string.Join("; ", valEx.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"));
                    var response = ApiResponse<object>.Fail("VALIDATION_ERROR", errors, "Validation failed");
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
                    _logger.LogWarning(invEx, "Business operation error: {Message}", invEx.Message);
                    var isUnavailable = invEx.Message.Contains("temporarily unavailable", StringComparison.OrdinalIgnoreCase);
                    var isConflict = invEx.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase)
                        || invEx.Message.Contains("already in progress", StringComparison.OrdinalIgnoreCase)
                        || invEx.Message.Contains("already checked out", StringComparison.OrdinalIgnoreCase)
                        || invEx.Message.Contains("already open", StringComparison.OrdinalIgnoreCase);

                    var statusCode = isUnavailable 
                        ? StatusCodes.Status503ServiceUnavailable
                        : (isConflict ? StatusCodes.Status409Conflict : StatusCodes.Status400BadRequest);

                    context.Response.StatusCode = statusCode;
                    var code = isUnavailable ? "SERVICE_UNAVAILABLE" : (isConflict ? "CONFLICT" : "BAD_REQUEST");
                    var userMsg = isUnavailable ? "Service temporarily unavailable" : (isConflict ? "Operation conflict" : "Invalid request");
                    var response = ApiResponse<object>.Fail(code, invEx.Message, userMsg);
                    var json = JsonSerializer.Serialize(response);
                    await context.Response.WriteAsync(json);
                    break;
                }

                case KeyNotFoundException notFoundEx:
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
