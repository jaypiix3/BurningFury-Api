using BurningFuryApi.Models;

namespace BurningFuryApi.Services;

public interface IFeedbackService
{
    Task SubmitAsync(Feedback feedback, string ipAddress, CancellationToken cancellationToken = default);
}
