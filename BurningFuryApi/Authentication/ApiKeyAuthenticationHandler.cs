using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace BurningFuryApi.Authentication;

public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string HeaderName = "X-Api-Key";
    private readonly HashSet<string> _validKeys;
    private readonly ILogger<ApiKeyAuthenticationHandler> _logger;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        IConfiguration configuration) : base(options, logger, encoder, clock)
    {
        _logger = logger.CreateLogger<ApiKeyAuthenticationHandler>();
        var keys = configuration.GetSection("ApiKeys:Keys").Get<string[]>() ?? Array.Empty<string>();
        _validKeys = new HashSet<string>(keys.Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim()));
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!_validKeys.Any())
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // Try header first
        if (!Request.Headers.TryGetValue(HeaderName, out var providedKey))
        {
            // Try query string fallback (?api_key=)
            if (!Request.Query.TryGetValue("api_key", out providedKey))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }
        }

        var key = providedKey.ToString();
        if (string.IsNullOrWhiteSpace(key) || !_validKeys.Contains(key))
        {
            _logger.LogWarning("Invalid API key attempt from {IP}", Context.Connection.RemoteIpAddress?.ToString());
            return Task.FromResult(AuthenticateResult.Fail("Invalid API Key"));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, $"apikey:{key.Substring(0, Math.Min(8, key.Length))}"),
            new(ClaimTypes.Name, "ApiKeyClient"),
            new("auth_type", "api_key")
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
