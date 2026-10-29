using System.Net.Mail;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SubscriberService.Data;
using SubscriberService.Dtos;
using SubscriberService.Models;

namespace SubscriberService.Controllers;

[ApiController]
[Route("[controller]")]
public class SubscriberController(SubscriberDbContextFactory factory) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SubscriberDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        await using var db = factory.Create();
        var subscribers = await db.Subscribers
            .AsNoTracking()
            .OrderBy(subscriber => subscriber.Email)
            .Select(subscriber => new SubscriberDto(
                subscriber.Id,
                subscriber.Email,
                subscriber.SubscribedAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(subscribers);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SubscriberDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        await using var db = factory.Create();
        var subscriber = await db.Subscribers
            .AsNoTracking()
            .Where(candidate => candidate.Id == id)
            .Select(candidate => new SubscriberDto(
                candidate.Id,
                candidate.Email,
                candidate.SubscribedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);

        return subscriber is null ? NotFound() : Ok(subscriber);
    }

    [HttpPost]
    public async Task<ActionResult<SubscriberDto>> Subscribe(
        [FromBody] CreateSubscriberRequestDto request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        if (!MailAddress.TryCreate(email, out var parsedEmail))
        {
            return BadRequest("A valid email address is required.");
        }

        email = parsedEmail.Address;
        var subscriber = new Subscriber
        {
            Id = Guid.NewGuid(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            SubscribedAtUtc = DateTimeOffset.UtcNow
        };
        var outboxMessage = new SubscriberOutboxMessage
        {
            MessageId = Guid.NewGuid(),
            SubscriberId = subscriber.Id,
            Email = subscriber.Email,
            SubscribedAtUtc = subscriber.SubscribedAtUtc
        };

        await using var db = factory.Create();
        db.Subscribers.Add(subscriber);
        db.OutboxMessages.Add(outboxMessage);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsDuplicateEmail(ex))
        {
            return Conflict("The email address is already subscribed.");
        }

        var response = new SubscriberDto(
            subscriber.Id,
            subscriber.Email,
            subscriber.SubscribedAtUtc);

        return CreatedAtAction(nameof(GetById), new { id = subscriber.Id }, response);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Unsubscribe(
        Guid id,
        CancellationToken cancellationToken)
    {
        await using var db = factory.Create();
        var subscriber = await db.Subscribers.FindAsync([id], cancellationToken);
        if (subscriber is null)
        {
            return NotFound();
        }

        var pendingMessages = await db.OutboxMessages
            .Where(message =>
                message.SubscriberId == id &&
                message.PublishedAtUtc == null)
            .ToListAsync(cancellationToken);

        db.OutboxMessages.RemoveRange(pendingMessages);
        db.Subscribers.Remove(subscriber);
        await db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private static bool IsDuplicateEmail(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };
}
