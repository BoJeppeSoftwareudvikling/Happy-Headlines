using Messaging.Contracts;
using Microsoft.AspNetCore.Mvc;
using PublisherService.Clients;
using PublisherService.Dtos;
using PublisherService.Messaging;
using RabbitMQ.Client.Exceptions;

namespace PublisherService.Controllers;

[ApiController]
[Route("[controller]")]
public class PublisherController(
    ProfanityClient profanityClient,
    ArticlePublisher articlePublisher,
    ILogger<PublisherController> logger) : ControllerBase
{
    private static readonly HashSet<string> Continents =
    [
        "Africa",
        "Asia",
        "Europe",
        "NorthAmerica",
        "SouthAmerica",
        "Oceania",
        "Antarctica",
        "Global"
    ];

    [HttpPost("articles")]
    public async Task<IActionResult> Publish(
        [FromBody] PublishArticleRequestDto request,
        CancellationToken cancellationToken)
    {
        var title = request.Title?.Trim() ?? string.Empty;
        var content = request.Content?.Trim() ?? string.Empty;
        var continent = request.Continent?.Trim() ?? string.Empty;

        if (request.MessageId == Guid.Empty)
        {
            return BadRequest("messageId is required and must remain stable when retrying a publication.");
        }

        if (title.Length == 0 || content.Length == 0)
        {
            return BadRequest("title and content are required.");
        }

        if (!Continents.Contains(continent))
        {
            return BadRequest($"Unsupported continent '{continent}'.");
        }

        try
        {
            var moderation = await profanityClient.CheckAsync(
                $"{title}\n{content}",
                cancellationToken);

            if (moderation.ContainsProfanity)
            {
                return BadRequest(new
                {
                    Message = "The article contains prohibited words.",
                    moderation.MatchedWords
                });
            }

            var article = new ArticlePublished
            {
                MessageId = request.MessageId,
                Title = title,
                Content = content,
                Continent = continent,
                PublishedAtUtc = DateTimeOffset.UtcNow
            };

            await articlePublisher.PublishAsync(article, cancellationToken);

            return Accepted(new
            {
                article.MessageId,
                article.PublishedAtUtc
            });
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Publishing dependency timed out.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                "A publishing dependency is unavailable.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutException or RabbitMQClientException)
        {
            logger.LogWarning(ex, "Publishing dependency is unavailable.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                "A publishing dependency is unavailable.");
        }
    }

    [HttpGet("instance")]
    public IActionResult Instance() => Ok(new { Hostname = Environment.MachineName });
}
