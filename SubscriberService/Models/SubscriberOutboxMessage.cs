namespace SubscriberService.Models;

public class SubscriberOutboxMessage
{
    public Guid MessageId { get; set; }
    public Guid SubscriberId { get; set; }
    public string Email { get; set; } = string.Empty;
    public DateTimeOffset SubscribedAtUtc { get; set; }
    public DateTimeOffset? PublishedAtUtc { get; set; }
}
