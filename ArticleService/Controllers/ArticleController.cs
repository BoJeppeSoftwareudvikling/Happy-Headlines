using ArticleService.Caching;
using ArticleService.Data;
using ArticleService.Models;
using ArticleService.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ArticleService.Controllers;

[ApiController]
[Route("[controller]")]
public class ArticleController(
    ArticleDbContextFactory factory,
    IArticleCache cache,
    IOptions<ArticleCacheOptions> cacheOptions) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Article>>> GetAll(
        [FromQuery] Continent continent,
        CancellationToken cancellationToken)
    {
        if (continent == Continent.Global)
        {
            var cachedArticles = await cache.GetAllAsync();

            if (cachedArticles is not null)
            {
                return Ok(cachedArticles);
            }

            var cutoff = DateTimeOffset.UtcNow
                .AddDays(-cacheOptions.Value.WindowDays);

            await using var globalDb = factory.Create(Continent.Global);

            var recentArticles = await globalDb.Articles
                .AsNoTracking()
                .Where(article => article.PublishedAtUtc >= cutoff)
                .OrderByDescending(article => article.PublishedAtUtc)
                .ToListAsync(cancellationToken);

            return Ok(recentArticles);
        }

        await using var db = factory.Create(continent);

        return Ok(await db.Articles
            .AsNoTracking()
            .ToListAsync(cancellationToken));
    }

    [HttpGet("instance")]
    public ActionResult<object> Instance() =>
        Ok(new { Hostname = Environment.MachineName });

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Article>> GetById(
        int id,
        [FromQuery] Continent continent,
        CancellationToken cancellationToken)
    {
        if (continent == Continent.Global)
        {
            var cachedArticle = await cache.GetAsync(id);

            if (cachedArticle is not null)
            {
                return Ok(cachedArticle);
            }
        }

        await using var db = factory.Create(continent);

        var article = await db.Articles.FindAsync(
            [id],
            cancellationToken);

        return article is null ? NotFound() : Ok(article);
    }

    [HttpPost]
    public async Task<ActionResult<Article>> Create(
        [FromBody] Article article,
        CancellationToken cancellationToken)
    {
        var toSave = new Article
        {
            Title = article.Title,
            Content = article.Content,
            Continent = article.Continent,
            PublishedAtUtc = DateTimeOffset.UtcNow
        };

        await using var db = factory.Create(toSave.Continent);
        db.Articles.Add(toSave);
        await db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = toSave.Id, continent = toSave.Continent },
            toSave);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromQuery] Continent continent,
        [FromBody] Article updated,
        CancellationToken cancellationToken)
    {
        await using var db = factory.Create(continent);
        var article = await db.Articles.FindAsync(
            [id],
            cancellationToken);

        if (article is null)
        {
            return NotFound();
        }

        article.Title = updated.Title;
        article.Content = updated.Content;
        await db.SaveChangesAsync(cancellationToken);

        if (continent == Continent.Global)
        {
            await cache.RemoveAsync(id);
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
        var article = await db.Articles.FindAsync(
            [id],
            cancellationToken);

        if (article is null)
        {
            return NotFound();
        }

        db.Articles.Remove(article);
        await db.SaveChangesAsync(cancellationToken);

        if (continent == Continent.Global)
        {
            await cache.RemoveAsync(id);
        }

        return NoContent();
    }
}
