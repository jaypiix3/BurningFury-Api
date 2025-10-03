using BurningFuryApi.Models;
using BurningFuryApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BurningFuryApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
[Tags("Feedback")]
[EnableRateLimiting("feedback")]
public class FeedbackController : ControllerBase
{
    private readonly IFeedbackService _feedbackService;
    private readonly ILogger<FeedbackController> _logger;

    public FeedbackController(IFeedbackService feedbackService, ILogger<FeedbackController> logger)
    {
        _feedbackService = feedbackService;
        _logger = logger;
    }

    /// <summary>
    /// Submit feedback (anonymous allowed)
    /// </summary>
    /// <param name="feedback">Feedback payload</param>
    /// <returns>Status</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Submit([FromBody] Feedback feedback, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
            await _feedbackService.SubmitAsync(feedback, ip, cancellationToken);
            return Accepted(new { message = "Feedback received" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting feedback");
            return StatusCode(500, new { error = "Internal error" });
        }
    }
}
