using System.Net.Http.Json;
using System.Text.Json;
using BurningFuryApi.Models;

namespace BurningFuryApi.Services;

public class FeedbackService : IFeedbackService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FeedbackService> _logger;
    private readonly IConfiguration _configuration;

    private const string WebhookConfigKey = "Feedback:DiscordWebhook";

    public FeedbackService(HttpClient httpClient, ILogger<FeedbackService> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task SubmitAsync(Feedback feedback, string ipAddress, CancellationToken cancellationToken = default)
    {
        var webhookUrl = _configuration[WebhookConfigKey];
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            _logger.LogWarning("Discord webhook url not configured at {Key}", WebhookConfigKey);
            return; // fail quietly
        }

        var displayName = feedback.Anonymous ? "Anonymous" : (string.IsNullOrWhiteSpace(feedback.Name) ? "Unknown" : feedback.Name.Trim());

        var embed = new
        {
            username = "Feedback Bot",
            embeds = new object[]
            {
                new
                {
                    title = "New Feedback Received",
                    color = 0xFFA500, // orange
                    fields = new object[]
                    {
                        new { name = "From", value = displayName, inline = true },
                        new { name = "Anonymous", value = feedback.Anonymous ? "Yes" : "No", inline = true },
                        //new { name = "IP", value = string.IsNullOrEmpty(ipAddress) ? "n/a" : ipAddress, inline = true },
                        new { name = "Message", value = Truncate(feedback.Message, 1000), inline = false },
                    },
                    timestamp = DateTime.UtcNow.ToString("O")
                }
            }
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, webhookUrl)
            {
                Content = JsonContent.Create(embed, options: new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                })
            };
            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to post feedback to discord. Status: {Status}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting feedback to discord");
        }
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value.Substring(0, max - 3) + "...";
}
