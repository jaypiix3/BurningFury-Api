using System.ComponentModel.DataAnnotations;

namespace BurningFuryApi.Models;

/// <summary>
/// Feedback submitted by a user or anonymous visitor.
/// </summary>
public class Feedback
{
    /// <summary>
    /// Optional name of the sender (ignored if Anonymous is true)
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Indicates whether the sender wishes to remain anonymous
    /// </summary>
    public bool Anonymous { get; set; }

    /// <summary>
    /// Feedback message body
    /// </summary>
    [Required]
    [StringLength(2000, MinimumLength = 3)]
    public string Message { get; set; } = string.Empty;
}
