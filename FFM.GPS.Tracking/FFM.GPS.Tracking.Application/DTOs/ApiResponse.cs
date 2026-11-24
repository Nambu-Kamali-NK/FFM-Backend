using System;

namespace FFM.GPS.Tracking.Application.DTOs
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public ApiError? Error { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public static ApiResponse<T> Ok(T data, string message = "Request processed successfully") =>
            new()
            {
                Success = true,
                Message = message,
                Data = data,
                Timestamp = DateTime.UtcNow
            };

        public static ApiResponse<T> Fail(string code, string details, string message = "An error occurred") =>
            new()
            {
                Success = false,
                Message = message,
                Error = new ApiError { Code = code, Details = details },
                Timestamp = DateTime.UtcNow
            };
    }

    public class ApiResponse : ApiResponse<object>
    {
        public static ApiResponse Ok(string message = "Request processed successfully") =>
            new()
            {
                Success = true,
                Message = message,
                Timestamp = DateTime.UtcNow
            };
    }

    public class ApiError
    {
        public string Code { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
    }
}
