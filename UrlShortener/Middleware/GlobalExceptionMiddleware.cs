using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.Json;
using StackExchange.Redis;

namespace UrlShortener.Middleware
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
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
                var correlationId = context.TraceIdentifier;
                _logger.LogError(ex, $"Unhandled exception | CorrelationId: {correlationId}");

                await HandleExceptionAsync(context, ex, correlationId);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception ex, string correlationId)
        {
            context.Response.ContentType = "application/json";
            var env = context.RequestServices.GetRequiredService<IWebHostEnvironment>();

            var statusCode = ex switch
            {
                ArgumentException => 400,
                KeyNotFoundException => 404,
                UnauthorizedAccessException => 401,
                DbUpdateException or RedisConnectionException => 503,
                _ => 500
            };

            context.Response.StatusCode = statusCode;

            var response = new
            {
                error = true,
                message = env.IsDevelopment() ? ex.Message : GetUserFriendlyMessage(statusCode),
                correlationId 
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }

        private static string GetUserFriendlyMessage(int statusCode) => statusCode switch
        {
            400 => "Bad request",
            404 => "Not found",
            401 => "Unauthorized",
            503 => "Service unavailable",
            _ => "Something went wrong. Support has been notified."
        };
    }
}
