using ArticleCache.Contracts;
using ArticleService.Caching;
using ArticleService.Data;
using ArticleService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Monitoring;

namespace ArticleService.Controllers;

[ApiController]
[Route("[controller]")]
public class ArticleController(
    ArticleDbContextFactory factory,
    IArticleCache cache,
    IOptions<ArticleCacheOptions> cacheOptions) : ControllerBase
{
    [HttpGet]
    public ActionResult<IEnumerable<Article>> GetAll([FromQuery] Continent continent)
    {
        using var db = factory.Create(continent);
        return Ok(db.Articles.ToList());
    }

    [HttpGet("instance")]
    public ActionResult<object> Instance() => Ok(new { Hostname = Environment.MachineName });

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Article>> GetById(
        int id,
        [FromQuery] Continent continent,
        CancellationToken cancellationToken)
    {
        if (continent == Continent.Global)
        {
            var cacheResult = await cache.GetByIdAsync(id, cancellationToken);

            if (cacheResult is
                { Outcome: ArticleCacheReadOutcome.Hit, Value: not null })
            {
                return Ok(ToArticle(cacheResult.Value));
            }

            CacheMetrics.RecordFallback(
                "article",
                cacheResult.Outcome == ArticleCacheReadOutcome.Miss
                    ? "miss"
                    : "error");
        }

        await using var db = factory.Create(continent);
        var article = await db.Articles.FindAsync([id], cancellationToken);
        return article is null ? NotFound() : Ok(article);
    }

    [HttpGet("recent")]
    public async Task<ActionResult<IEnumerable<Article>>> GetRecent(
        [FromQuery] Continent? continent,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        if (continent is null)
        {
            return BadRequest("continent is required.");
        }

        if (continent != Continent.Global)
        {
            return BadRequest("Recent cached reads currently support only Global.");
        }

        var requestedLimit = limit ?? cacheOptions.Value.DefaultRecentLimit;
        if (requestedLimit <= 0)
        {
            return BadRequest("limit must be greater than 0.");
        }

        var effectiveLimit = Math.Min(
            requestedLimit,
            cacheOptions.Value.MaximumRecentLimit);

        var cacheResult = await cache.GetRecentAsync(
            effectiveLimit,
            cancellationToken);

        if (cacheResult is
            { Outcome: ArticleCacheReadOutcome.Hit, Value: not null })
        {
            return Ok(cacheResult.Value.Select(ToArticle));
        }

        CacheMetrics.RecordFallback(
            "article",
            cacheResult.Outcome == ArticleCacheReadOutcome.Miss
                ? "miss"
                : "error");

        var cutoff = DateTimeOffset.UtcNow.AddDays(-14);
        await using var db = factory.Create(Continent.Global);
        var articles = await db.Articles
            .Where(article => article.PublishedAtUtc >= cutoff)
            .OrderByDescending(article => article.PublishedAtUtc)
            .ThenByDescending(article => article.Id)
            .Take(effectiveLimit)
            .ToListAsync(cancellationToken);

        return Ok(articles);
    }

    [HttpPost]
    public ActionResult<Article> Create([FromBody] Article article)
    {
        var toSave = new Article
        {
            Title = article.Title,
            Content = article.Content,
            Continent = article.Continent,
            PublishedAtUtc = DateTimeOffset.UtcNow
        };

        using var db = factory.Create(toSave.Continent);
        db.Articles.Add(toSave);
        db.SaveChanges();
        return CreatedAtAction(nameof(GetById),
            new { id = toSave.Id, continent = toSave.Continent }, toSave);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromQuery] Continent continent,
        [FromBody] Article updated,
        CancellationToken cancellationToken)
    {
        await using var db = factory.Create(continent);
        var article = await db.Articles.FindAsync([id], cancellationToken);
        if (article is null) return NotFound();

        article.Title = updated.Title;
        article.Content = updated.Content;
        await db.SaveChangesAsync(cancellationToken);

        if (continent == Continent.Global)
        {
            await cache.InvalidateAsync(id, cancellationToken);
        }

        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(
        int id,
        [FromQuery] Continent continent,
        CancellationToken cancellationToken)
    {
        await using var db = factory.Create(continent);
        var article = await db.Articles.FindAsync([id], cancellationToken);
        if (article is null) return NotFound();

        db.Articles.Remove(article);
        await db.SaveChangesAsync(cancellationToken);

        if (continent == Continent.Global)
        {
            await cache.InvalidateAsync(id, cancellationToken);
        }

        return NoContent();
    }

    private static Article ToArticle(CachedArticle cached) => new()
    {
        Id = cached.Id,
        Title = cached.Title,
        Content = cached.Content,
        Continent = Enum.Parse<Continent>(cached.Continent),
        PublishedAtUtc = cached.PublishedAtUtc
    };
}
