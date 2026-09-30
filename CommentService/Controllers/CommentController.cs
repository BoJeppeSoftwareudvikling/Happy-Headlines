using CommentService.Caching;
using CommentService.Clients;
using CommentService.Data;
using CommentService.Dtos;
using CommentService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Monitoring;

namespace CommentService.Controllers;

[ApiController]
[Route("[controller]")]
public class CommentController(
    CommentDbContextFactory factory,
    ICommentCache cache,
    ProfanityClient profanityClient,
    ILogger<CommentController> logger) : ControllerBase
{
    [HttpGet]
    public ActionResult<IEnumerable<Comment>> GetAll()
    {
        using var db = factory.Create();
        return Ok(db.Comments.ToList());
    }

    [HttpGet("article/{articleId:int}")]
    public async Task<ActionResult<IEnumerable<Comment>>> GetByArticle(
        int articleId,
        CancellationToken cancellationToken)
    {
        var cacheResult = await cache.GetAsync(articleId, cancellationToken);
        if (cacheResult is
            { Outcome: CommentCacheReadOutcome.Hit, Value: not null })
        {
            return Ok(cacheResult.Value);
        }

        await using var db = factory.Create();
        var comments = await db.Comments
            .Where(comment => comment.ArticleId == articleId)
            .OrderBy(comment => comment.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        CacheMetrics.RecordFallback(
            "comment",
            cacheResult.Outcome == CommentCacheReadOutcome.Miss
                ? "miss"
                : "error");

        if (cacheResult.Outcome == CommentCacheReadOutcome.Miss)
        {
            await cache.StoreAsync(articleId, comments, cancellationToken);
        }

        return Ok(comments);
    }

    [HttpGet("{id:int}")]
    public ActionResult<Comment> GetById(int id)
    {
        using var db = factory.Create();
        var comment = db.Comments.Find(id);
        return comment is null ? NotFound() : Ok(comment);
    }

    [HttpPost]
    public async Task<ActionResult<Comment>> Create(
        [FromBody] CreateCommentRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.ArticleId <= 0)
        {
            return BadRequest("articleId must be greater than 0.");
        }

        if (string.IsNullOrWhiteSpace(request.AuthorName))
        {
            return BadRequest("authorName is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest("content is required.");
        }

        var content = request.Content.Trim();
        var status = ModerationStatus.Filtered;

        try
        {
            var moderation = await profanityClient.CheckProfanityAsync(content, cancellationToken);
            content = moderation.FilteredText;
        }
        catch (ProfanityServiceUnavailableException ex)
            when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                ex,
                "Profanity filtering marked down. Storing comment as Unmoderated.");
            status = ModerationStatus.Unmoderated;
        }

        var toSave = new Comment
        {
            ArticleId = request.ArticleId,
            AuthorName = request.AuthorName.Trim(),
            Content = content,
            CreatedAtUtc = DateTime.UtcNow,
            ModerationStatus = status
        };

        await using var db = factory.Create();
        db.Comments.Add(toSave);
        await db.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(toSave.ArticleId, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = toSave.Id }, toSave);
    }
}