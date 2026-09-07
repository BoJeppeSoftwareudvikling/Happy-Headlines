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
    IProfanityClient profanityClient,
    ILogger<CommentController> logger) : ControllerBase
{
    [HttpGet("instance")]
    public ActionResult<object> Instance() => Ok(new { Hostname = Environment.MachineName });

    [HttpGet("article/{articleId:int}")]
    public async Task<ActionResult<IEnumerable<Comment>>> GetByArticle(
        int articleId,
        [FromQuery] Continent continent,
        CancellationToken cancellationToken)
    {
        if (articleId <= 0)
        {
            return BadRequest("articleId must be greater than 0.");
        }

        await using var db = factory.Create();

        var comments = await db.Comments
            .AsNoTracking()
            .Where(comment => comment.ArticleId == articleId && comment.Continent == continent)
            .OrderBy(comment => comment.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return Ok(comments);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Comment>> GetById(int id, CancellationToken cancellationToken)
    {
        await using var db = factory.Create();

        var comment = await db.Comments.FindAsync(new object[] { id }, cancellationToken);

        return comment is null ? NotFound() : Ok(comment);
    }

    [HttpPost]
    public async Task<ActionResult<Comment>> Create(
        [FromBody] CreateCommentRequest request,
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

        ProfanityCheckResponse moderation;

        try
        {
            moderation = await profanityClient.CheckAsync(request.Content, cancellationToken);
        }
        catch (ProfanityServiceUnavailableException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                ex,
                "Comment could not be accepted because profanity filtering is unavailable.");

            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    Message = "Comments cannot be accepted while moderation is unavailable. Try again later."
                });
        }

        var toSave = new Comment
        {
            ArticleId = request.ArticleId,
            Continent = request.Continent,
            AuthorName = request.AuthorName.Trim(),
            Content = moderation.FilteredText,
            CreatedAtUtc = DateTime.UtcNow
        };

        await using var db = factory.Create();

        db.Comments.Add(toSave);
        await db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = toSave.Id }, toSave);
    }
}
