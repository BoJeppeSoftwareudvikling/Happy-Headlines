using System.Net.Http.Json;
using CommentService.Dtos;
using Polly.CircuitBreaker;

namespace CommentService.Clients;

public interface IProfanityClient
{
    Task<ProfanityCheckResponse> CheckAsync(string text, CancellationToken cancellationToken);
}

public sealed class ProfanityClient(
    HttpClient httpClient,
    ILogger<ProfanityClient> logger) : IProfanityClient
{
    public async Task<ProfanityCheckResponse> CheckAsync(string text, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                "Profanity/check",
                new ProfanityCheckRequest { Text = text },
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ProfanityCheckResponse>(
                cancellationToken: cancellationToken);

            if (result is null)
            {
                throw new HttpRequestException("ProfanityService returned an empty response.");
            }

            return result;
        }
        catch (BrokenCircuitException ex)
        {
            logger.LogWarning(ex, "ProfanityService circuit breaker is open.");
            throw new ProfanityServiceUnavailableException("ProfanityService circuit breaker is open.", ex);
        }
        catch (Exception ex) when (IsTransientDependencyFailure(ex, cancellationToken))
        {
            logger.LogWarning(ex, "ProfanityService is unavailable.");
            throw new ProfanityServiceUnavailableException("ProfanityService is unavailable.", ex);
        }
    }

    private static bool IsTransientDependencyFailure(Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return exception is HttpRequestException
            or TaskCanceledException
            or TimeoutException;
    }
}

public sealed class ProfanityServiceUnavailableException : Exception
{
    public ProfanityServiceUnavailableException(string message, Exception innerException)
        : base(message, innerException) { }
}
