using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace BurningFuryApi.Examples
{
    /// <summary>
    /// Example client demonstrating how to use the BurningFury API with Auth0 authentication
    /// </summary>
    public class BurningFuryApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private string? _accessToken;

        public BurningFuryApiClient(string baseUrl)
        {
            _httpClient = new HttpClient();
            _baseUrl = baseUrl.TrimEnd('/');
        }

        /// <summary>
        /// Authenticate with Auth0 and get an access token
        /// </summary>
        /// <param name="domain">Your Auth0 domain</param>
        /// <param name="clientId">Your Auth0 client ID</param>
        /// <param name="clientSecret">Your Auth0 client secret</param>
        /// <param name="audience">Your API audience/identifier</param>
        /// <returns>True if authentication was successful</returns>
        public async Task<bool> AuthenticateAsync(string domain, string clientId, string clientSecret, string audience)
        {
            try
            {
                var tokenRequest = new
                {
                    client_id = clientId,
                    client_secret = clientSecret,
                    audience = audience,
                    grant_type = "client_credentials"
                };

                var json = JsonSerializer.Serialize(tokenRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"https://{domain}/oauth/token", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var tokenResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    
                    _accessToken = tokenResponse.GetProperty("access_token").GetString();
                    _httpClient.DefaultRequestHeaders.Authorization = 
                        new AuthenticationHeaderValue("Bearer", _accessToken);
                    
                    return true;
                }
                
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Test the API health endpoint (no authentication required)
        /// </summary>
        public async Task<string> GetHealthAsync()
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/auth/health");
            return await response.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// Validate the current token
        /// </summary>
        public async Task<string> ValidateTokenAsync()
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/auth/validate");
            return await response.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// Get all players
        /// </summary>
        public async Task<string> GetAllPlayersAsync()
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/players");
            return await response.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// Get a specific player by ID
        /// </summary>
        public async Task<string> GetPlayerAsync(Guid playerId)
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/players/{playerId}");
            return await response.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// Create a new player
        /// </summary>
        public async Task<string> CreatePlayerAsync(string name, string region, string realm)
        {
            var player = new
            {
                name = name,
                region = region,
                realm = realm
            };

            var json = JsonSerializer.Serialize(player);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/players", content);
            return await response.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// Delete a player
        /// </summary>
        public async Task<bool> DeletePlayerAsync(Guid playerId)
        {
            var response = await _httpClient.DeleteAsync($"{_baseUrl}/api/players/{playerId}");
            return response.IsSuccessStatusCode;
        }

        /// <summary>
        /// Get current user information
        /// </summary>
        public async Task<string> GetCurrentUserAsync()
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/players/me");
            return await response.Content.ReadAsStringAsync();
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    /// <summary>
    /// Example usage of the BurningFury API client
    /// </summary>
    public class ApiClientExample
    {
        public static async Task RunExample()
        {
            // Configuration - replace with your actual values
            const string baseUrl = "https://localhost:7000"; // Your API URL
            const string auth0Domain = "your-domain.auth0.com";
            const string clientId = "your-client-id";
            const string clientSecret = "your-client-secret";
            const string audience = "https://burningfury.api";

            using var client = new BurningFuryApiClient(baseUrl);

            try
            {
                // Test public endpoint
                Console.WriteLine("Testing health endpoint...");
                var health = await client.GetHealthAsync();
                Console.WriteLine($"Health: {health}");

                // Authenticate
                Console.WriteLine("Authenticating with Auth0...");
                var authenticated = await client.AuthenticateAsync(auth0Domain, clientId, clientSecret, audience);
                
                if (!authenticated)
                {
                    Console.WriteLine("Authentication failed!");
                    return;
                }

                Console.WriteLine("Authentication successful!");

                // Validate token
                Console.WriteLine("Validating token...");
                var validation = await client.ValidateTokenAsync();
                Console.WriteLine($"Token validation: {validation}");

                // Get current user
                Console.WriteLine("Getting current user...");
                var currentUser = await client.GetCurrentUserAsync();
                Console.WriteLine($"Current user: {currentUser}");

                // Create a player
                Console.WriteLine("Creating a new player...");
                var createResult = await client.CreatePlayerAsync("TestPlayer", "US", "Stormrage");
                Console.WriteLine($"Create result: {createResult}");

                // Get all players
                Console.WriteLine("Getting all players...");
                var players = await client.GetAllPlayersAsync();
                Console.WriteLine($"Players: {players}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}