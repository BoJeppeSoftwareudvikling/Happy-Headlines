using System.Net.Http.Json;
using PublisherService.Dtos;

namespace PublisherService.Clients;

public class ProfanityClient(HttpClient httpClient)
{
    public async Task<ProfanityCheckResponseDto> CheckAsync(
        string text,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "Profanity/check",
            new { Text = text },
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ProfanityCheckResponseDto>(
                   cancellationToken: cancellationToken)
               ?? throw new HttpRequestException("ProfanityService returned an empty response.");
    }
}
