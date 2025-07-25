using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using BurningFuryApi.Services;

namespace BurningFuryApi.Controllers
{
    /// <summary>
    /// Controller for authentication-related endpoints
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [Tags("Authentication")]
    public class AuthController : ControllerBase
    {
        private readonly ILogger<AuthController> _logger;
        private readonly IPlayerService _playerService;

        public AuthController(ILogger<AuthController> logger, IPlayerService playerService)
        {
            _logger = logger;
            _playerService = playerService;
        }

        /// <summary>
        /// Public endpoint to test if the API is running
        /// </summary>
        /// <returns>A simple health check message</returns>
        /// <response code="200">Returns a health check message</response>
        [HttpGet("health")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public async Task<ActionResult> GetHealth()
        {
            try
            {
                var dbHealth = await _playerService.CheckDatabaseConnectionAsync();
                
                return Ok(new
                {
                    Status = "Healthy",
                    Timestamp = DateTime.UtcNow,
                    Message = "BurningFury API is running",
                    Database = dbHealth ? "Connected" : "Disconnected",
                    Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed");
                return StatusCode(500, new
                {
                    Status = "Unhealthy",
                    Timestamp = DateTime.UtcNow,
                    Message = "Health check failed",
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Protected endpoint to validate JWT token
        /// </summary>
        /// <returns>Token validation result and user claims</returns>
        /// <response code="200">Returns token validation success with user claims</response>
        /// <response code="401">If the token is invalid or missing</response>
        [HttpGet("validate")]
        [Authorize]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public ActionResult ValidateToken()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? 
                           User.FindFirst("sub")?.Value;
                var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
                var userName = User.FindFirst(ClaimTypes.Name)?.Value;
                
                _logger.LogInformation("Token validated for user {UserId}", userId);

                return Ok(new
                {
                    Valid = true,
                    UserId = userId,
                    Email = userEmail,
                    Name = userName,
                    IssuedAt = User.FindFirst("iat")?.Value,
                    ExpiresAt = User.FindFirst("exp")?.Value,
                    Audience = User.FindFirst("aud")?.Value,
                    Issuer = User.FindFirst("iss")?.Value,
                    AllClaims = User.Claims.Select(c => new { c.Type, c.Value }).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating token");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Returns Auth0 configuration information for client applications
        /// </summary>
        /// <returns>Auth0 configuration details</returns>
        /// <response code="200">Returns Auth0 configuration</response>
        [HttpGet("config")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public ActionResult GetAuthConfig()
        {
            var domain = HttpContext.RequestServices
                .GetRequiredService<IConfiguration>()
                .GetSection("Auth0:Domain").Value;

            var audience = HttpContext.RequestServices
                .GetRequiredService<IConfiguration>()
                .GetSection("Auth0:Audience").Value;

            return Ok(new
            {
                Domain = domain,
                Audience = audience,
                TokenEndpoint = !string.IsNullOrEmpty(domain) ? $"https://{domain}/oauth/token" : null,
                AuthorizeEndpoint = !string.IsNullOrEmpty(domain) ? $"https://{domain}/authorize" : null,
                UserInfoEndpoint = !string.IsNullOrEmpty(domain) ? $"https://{domain}/userinfo" : null,
                JwksUri = !string.IsNullOrEmpty(domain) ? $"https://{domain}/.well-known/jwks.json" : null
            });
        }

        /// <summary>
        /// Test endpoint for debugging anonymous access
        /// </summary>
        /// <returns>Test message to verify anonymous access works</returns>
        /// <response code="200">Returns test message</response>
        [HttpGet("test-anonymous")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public ActionResult TestAnonymousAccess()
        {
            _logger.LogInformation("Anonymous test endpoint accessed");
            
            return Ok(new
            {
                Message = "Anonymous access is working!",
                Timestamp = DateTime.UtcNow,
                IsAuthenticated = User?.Identity?.IsAuthenticated ?? false,
                RequestPath = HttpContext.Request.Path,
                Method = HttpContext.Request.Method,
                HasAuthorizationHeader = Request.Headers.ContainsKey("Authorization"),
                AuthorizationHeader = Request.Headers.ContainsKey("Authorization") ? 
                    Request.Headers["Authorization"].ToString().Substring(0, Math.Min(20, Request.Headers["Authorization"].ToString().Length)) + "..." : 
                    "None"
            });
        }

        /// <summary>
        /// Debug endpoint to test token parsing without authentication requirement
        /// </summary>
        /// <returns>Token information if present</returns>
        /// <response code="200">Returns token debug information</response>
        [HttpGet("debug-token")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public ActionResult DebugToken()
        {
            _logger.LogInformation("Debug token endpoint accessed");
            
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            var hasToken = !string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ");
            
            return Ok(new
            {
                Message = "Token debug endpoint",
                Timestamp = DateTime.UtcNow,
                HasAuthorizationHeader = Request.Headers.ContainsKey("Authorization"),
                HasBearerToken = hasToken,
                IsAuthenticated = User?.Identity?.IsAuthenticated ?? false,
                UserClaims = User?.Claims?.Select(c => new { c.Type, c.Value }) ?? Enumerable.Empty<object>(),
                TokenPrefix = hasToken ? authHeader?.Substring(0, Math.Min(30, authHeader.Length)) + "..." : "None"
            });
        }
    }
}