using System.Net.Http.Json;
using CommentService.Dtos;
using Polly.CircuitBreaker;

namespace CommentService.Clients;

public class ProfanityServiceUnavailableException : Exception
{
    public ProfanityServiceUnavailableException(string message, Exception innerException) : base(message, innerException) { }
}

public class ProfanityClient(HttpClient httpClient, ILogger<ProfanityClient> logger)
{
    public async Task<ProfanityCheckResponseDto> CheckProfanityAsync(string text, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                "Profanity/check",
                new ProfanityCheckRequestDto { Text = text },
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ProfanityCheckResponseDto>(
                cancellationToken: cancellationToken);

            if (result is null)
            {
                throw new HttpRequestException("ProfanityService returned an empty response.");
            }

            return result;
        }
        catch (BrokenCircuitException ex)
        {
            logger.LogWarning(ex, "Profanity service is unavailable: {ErrorMessage}", ex.Message);
            throw new ProfanityServiceUnavailableException("Profanity service is unavailable", ex);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException)
        {
            logger.LogWarning(ex, "Error checking profanity: {ErrorMessage}", ex.Message);
            throw new ProfanityServiceUnavailableException("Error checking profanity", ex);
        }
    }
}