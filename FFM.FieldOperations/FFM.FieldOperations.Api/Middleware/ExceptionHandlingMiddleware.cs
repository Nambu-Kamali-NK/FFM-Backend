using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using FFM.FieldOperations.Application.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace FFM.FieldOperations.Api.Middleware
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
                    _logger.LogWarning(argEx, "Validation error: {Message}", argEx.Message);
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    var response = ApiResponse<object>.Fail("VALIDATION_ERROR", argEx.Message, "Validation error");
                    await context.Response.WriteAsync(JsonSerializer.Serialize(response));
                    break;
                }

                case UnauthorizedAccessException authEx:
                {
                    _logger.LogWarning(authEx, "Authorization failure: {Message}", authEx.Message);
                    context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    var response = ApiResponse<object>.Fail("FORBIDDEN", authEx.Message, "You do not have permission to perform this action");
                    await context.Response.WriteAsync(JsonSerializer.Serialize(response));
                    break;
                }

                case KeyNotFoundException notFoundEx:
                {
                    _logger.LogWarning(notFoundEx, "Resource not found: {Message}", notFoundEx.Message);
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    var response = ApiResponse<object>.Fail("NOT_FOUND", notFoundEx.Message, "Resource not found");
                    await context.Response.WriteAsync(JsonSerializer.Serialize(response));
                    break;
                }

                case InvalidOperationException invEx:
                {
                    _logger.LogWarning(invEx, "Business operation conflict/invalid state: {Message}", invEx.Message);
                    var isConflict = invEx.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase);
                    var isInactiveSite = invEx.Message.Contains("inactive", StringComparison.OrdinalIgnoreCase);
                    var isInvalidState = invEx.Message.Contains("Cannot reassign", StringComparison.OrdinalIgnoreCase) ||
                                         invEx.Message.Contains("status", StringComparison.OrdinalIgnoreCase);

                    var statusCode = isConflict ? StatusCodes.Status409Conflict : StatusCodes.Status400BadRequest;
                    var code = isConflict ? "CONFLICT" : (isInactiveSite ? "CLIENT_SITE_INACTIVE" : (isInvalidState ? "INVALID_TASK_STATE" : "BAD_REQUEST"));

                    context.Response.StatusCode = statusCode;
                    var response = ApiResponse<object>.Fail(code, invEx.Message, "Business logic validation failure");
                    await context.Response.WriteAsync(JsonSerializer.Serialize(response));
                    break;
                }

                default:
                {
                    _logger.LogError(ex, "Unhandled server error occurred: {Message}", ex.Message);
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    var response = ApiResponse<object>.Fail("INTERNAL_SERVER_ERROR", "An unexpected error occurred while processing your request.", "Internal server error");
                    await context.Response.WriteAsync(JsonSerializer.Serialize(response));
                    break;
                }
            }
        }
    }
}
