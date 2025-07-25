using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;

namespace BurningFuryApi.Middleware
{
    /// <summary>
    /// Middleware for handling JWT authentication errors and providing better error responses
    /// </summary>
    public class JwtErrorHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<JwtErrorHandlingMiddleware> _logger;

        public JwtErrorHandlingMiddleware(RequestDelegate next, ILogger<JwtErrorHandlingMiddleware> logger)
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
                _logger.LogError(ex, "An unhandled exception occurred");
                await HandleExceptionAsync(context, ex);
            }

            // Handle authentication failures ONLY for endpoints that require authentication
            if (context.Response.StatusCode == 401)
            {
                // Check if the endpoint allows anonymous access
                var endpoint = context.GetEndpoint();
                var allowAnonymous = endpoint?.Metadata?.GetMetadata<IAllowAnonymous>() != null;
                
                // Only handle 401 if the endpoint requires authentication
                if (!allowAnonymous)
                {
                    await HandleUnauthorizedAsync(context);
                }
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var response = new
            {
                StatusCode = context.Response.StatusCode,
                Message = "An internal server error occurred.",
                Details = exception.Message
            };

            var jsonResponse = JsonSerializer.Serialize(response);
            await context.Response.WriteAsync(jsonResponse);
        }

        private static async Task HandleUnauthorizedAsync(HttpContext context)
        {
            if (!context.Response.HasStarted)
            {
                context.Response.ContentType = "application/json";
                
                var response = new
                {
                    StatusCode = 401,
                    Message = "Authentication failed. Please provide a valid JWT token.",
                    Details = "The request requires authentication. Include a valid Bearer token in the Authorization header."
                };

                var jsonResponse = JsonSerializer.Serialize(response);
                await context.Response.WriteAsync(jsonResponse);
            }
        }
    }

    /// <summary>
    /// Extension method to add JWT error handling middleware
    /// </summary>
    public static class JwtErrorHandlingMiddlewareExtensions
    {
        public static IApplicationBuilder UseJwtErrorHandling(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<JwtErrorHandlingMiddleware>();
        }
    }
}