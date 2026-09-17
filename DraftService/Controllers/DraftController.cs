using DraftService.Data;
using DraftService.Dtos;
using DraftService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Monitoring;

namespace DraftService.Controllers;

[ApiController]
[Route("[controller]")]
public class DraftController(DraftDbContextFactory factory) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Draft>>> GetAll(
        [FromQuery] string? publisherId,
        CancellationToken cancellationToken)
    {
        using var activity = MonitorService.ActivitySource.StartActivity("GetDrafts");

        var normalizedPublisherId = publisherId?.Trim();
        await using var db = factory.Create();
        var query = db.Drafts.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(publisherId))
        {
            MonitorService.Log.Here().Debug("Entered if statement");
            query = query.Where(draft => draft.PublisherId == normalizedPublisherId);

        }

        var drafts = await query
            .OrderByDescending(draft => draft.UpdatedAtUtc)
            .ToListAsync(cancellationToken);

        activity?.SetTag("draft.publisher_id", normalizedPublisherId);
        MonitorService.Log
            .Here()
            .Information(
                "Fetching drafts for publisher {PublisherId}",
                normalizedPublisherId);

        return Ok(drafts);
    }

    [HttpGet("instance")]
    public ActionResult<object> Instance() => Ok(new { Hostname = Environment.MachineName });

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Draft>> GetById(int id, CancellationToken cancellationToken)
    {
        await using var db = factory.Create();
        var draft = await db.Drafts
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        return draft is null ? NotFound() : Ok(draft);
    }

    [HttpPost]
    public async Task<ActionResult<Draft>> Create(
        [FromBody] CreateDraftRequestDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PublisherId))
        {
            return BadRequest("publisherId is required.");
        }

        var now = DateTime.UtcNow;
        var draft = new Draft
        {
            PublisherId = request.PublisherId.Trim(),
            Title = request.Title.Trim(),
            Content = request.Content,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await using var db = factory.Create();
        db.Drafts.Add(draft);
        await db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = draft.Id }, draft);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateDraftRequestDto request,
        CancellationToken cancellationToken)
    {
        await using var db = factory.Create();
        var draft = await db.Drafts.FindAsync([id], cancellationToken);
        if (draft is null)
        {
            return NotFound();
        }

        draft.Title = request.Title.Trim();
        draft.Content = request.Content;
        draft.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await using var db = factory.Create();
        var draft = await db.Drafts.FindAsync([id], cancellationToken);
        if (draft is null)
        {
            return NotFound();
        }

        db.Drafts.Remove(draft);
        await db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
