namespace BurningFuryApi.Configuration
{
    /// <summary>
    /// Configuration settings for Auth0 integration
    /// </summary>
    public class Auth0Settings
    {
        /// <summary>
        /// Auth0 domain (e.g., your-domain.auth0.com)
        /// </summary>
        public string Domain { get; set; } = string.Empty;

        /// <summary>
        /// Auth0 API identifier/audience
        /// </summary>
        public string Audience { get; set; } = string.Empty;
    }
}