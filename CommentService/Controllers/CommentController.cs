using CommentService.Caching;
using CommentService.Clients;
using CommentService.Data;
using CommentService.Dtos;
using CommentService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CommentService.Controllers;

[ApiController]
[Route("[controller]")]
public class CommentController(
    CommentDbContextFactory factory,
    ProfanityClient profanityClient,
    ICommentCache cache,
    ILogger<CommentController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Comment>>> GetAll(
        CancellationToken cancellationToken)
    {
        await using var db = factory.Create();

        return Ok(await db.Comments
            .AsNoTracking()
            .ToListAsync(cancellationToken));
    }

    [HttpGet("article/{articleId:int}")]
    public async Task<ActionResult<IEnumerable<Comment>>> GetByArticle(
        int articleId,
        CancellationToken cancellationToken)
    {
        if (articleId <= 0)
        {
            return BadRequest(
                "articleId must be greater than 0.");
        }

        var cachedComments = await cache.GetAsync(articleId);

        if (cachedComments is not null)
        {
            return Ok(cachedComments);
        }

        await using var db = factory.Create();

        var comments = await db.Comments
            .AsNoTracking()
            .Where(comment => comment.ArticleId == articleId)
            .OrderBy(comment => comment.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        await cache.SetAsync(articleId, comments);

        return Ok(comments);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Comment>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        await using var db = factory.Create();

        var comment = await db.Comments.FindAsync(
            [id],
            cancellationToken);

        return comment is null ? NotFound() : Ok(comment);
    }

    [HttpPost]
    public async Task<ActionResult<Comment>> Create(
        [FromBody] CreateCommentRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.ArticleId <= 0)
        {
            return BadRequest(
                "articleId must be greater than 0.");
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
            var moderation =
                await profanityClient.CheckProfanityAsync(
                    content,
                    cancellationToken);

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

        await cache.RemoveAsync(toSave.ArticleId);

        return CreatedAtAction(
            nameof(GetById),
            new { id = toSave.Id },
            toSave);
    }
}
