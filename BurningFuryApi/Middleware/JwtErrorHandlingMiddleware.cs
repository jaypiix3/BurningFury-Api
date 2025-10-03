using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using BurningFuryApi.Authentication;

namespace BurningFuryApi.Middleware
{
    /// <summary>
    /// Middleware for handling authentication errors and providing better error responses (JWT or API Key)
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
                var endpoint = context.GetEndpoint();
                var allowAnonymous = endpoint?.Metadata?.GetMetadata<IAllowAnonymous>() != null;
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
            if (context.Response.HasStarted) return;

            context.Response.ContentType = "application/json";

            var hasApiKey = context.Request.Headers.ContainsKey(ApiKeyAuthenticationHandler.HeaderName) ||
                            context.Request.Query.ContainsKey("api_key");

            string message;
            string details;

            if (hasApiKey)
            {
                // API key supplied but authentication failed
                message = "Authentication failed. Invalid API Key or token.";
                details = $"Provide a valid '{ApiKeyAuthenticationHandler.HeaderName}' header or a valid Bearer token in the Authorization header.";
            }
            else
            {
                message = "Authentication required.";
                details = $"Include either a Bearer token in the Authorization header or supply a '{ApiKeyAuthenticationHandler.HeaderName}' header.";
            }

            var response = new
            {
                StatusCode = 401,
                Message = message,
                Details = details
            };

            var jsonResponse = JsonSerializer.Serialize(response);
            await context.Response.WriteAsync(jsonResponse);
        }
    }

    /// <summary>
    /// Extension method to add authentication error handling middleware
    /// </summary>
    public static class JwtErrorHandlingMiddlewareExtensions
    {
        public static IApplicationBuilder UseJwtErrorHandling(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<JwtErrorHandlingMiddleware>();
        }
    }
}