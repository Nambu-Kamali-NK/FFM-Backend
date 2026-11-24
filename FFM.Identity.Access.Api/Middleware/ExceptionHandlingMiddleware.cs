using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace FFM.Identity.Access.Api.Middleware
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
            context.Response.ContentType = "application/problem+json";

            switch (ex)
            {
                case UnauthorizedAccessException authEx:
                {
                    _logger.LogWarning(authEx, "Authorization failure: {Message}", authEx.Message);
                    context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    var problem = new ProblemDetails
                    {
                        Type = "https://tools.ietf.org/html/rfc9110#section-15.5.4",
                        Title = "Forbidden",
                        Status = StatusCodes.Status403Forbidden,
                        Detail = authEx.Message.Contains("permission", StringComparison.OrdinalIgnoreCase) ||
                                 authEx.Message.Contains("admin", StringComparison.OrdinalIgnoreCase)
                            ? authEx.Message
                            : "You do not have permission to perform this action."
                    };

                    var json = JsonSerializer.Serialize(problem);
                    await context.Response.WriteAsync(json);
                    break;
                }

                case InvalidOperationException invEx:
                {
                    _logger.LogWarning(invEx, "Business operation error: {Message}", invEx.Message);
                    var statusCode = invEx.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase)
                        ? StatusCodes.Status409Conflict
                        : StatusCodes.Status400BadRequest;

                    context.Response.StatusCode = statusCode;
                    var problem = new ProblemDetails
                    {
                        Type = statusCode == StatusCodes.Status409Conflict
                            ? "https://tools.ietf.org/html/rfc9110#section-15.5.10"
                            : "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                        Title = statusCode == StatusCodes.Status409Conflict ? "Conflict" : "Bad Request",
                        Status = statusCode,
                        Detail = invEx.Message
                    };

                    var json = JsonSerializer.Serialize(problem);
                    await context.Response.WriteAsync(json);
                    break;
                }

                case ArgumentException argEx:
                {
                    _logger.LogWarning(argEx, "Invalid argument: {Message}", argEx.Message);
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    var problem = new ProblemDetails
                    {
                        Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                        Title = "Bad Request",
                        Status = StatusCodes.Status400BadRequest,
                        Detail = argEx.Message
                    };

                    var json = JsonSerializer.Serialize(problem);
                    await context.Response.WriteAsync(json);
                    break;
                }

                default:
                {
                    _logger.LogError(ex, "Unhandled server error: {Message}", ex.Message);
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

                    var problem = new ProblemDetails
                    {
                        Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
                        Title = "An error occurred while processing your request.",
                        Status = StatusCodes.Status500InternalServerError,
                        Detail = "An unexpected internal server error occurred."
                    };

                    var json = JsonSerializer.Serialize(problem);
                    await context.Response.WriteAsync(json);
                    break;
                }
            }
        }
    }
}
