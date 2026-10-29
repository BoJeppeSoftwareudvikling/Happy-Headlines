namespace Messaging.Contracts;

public class SubscriberRegistered
{
    public Guid MessageId { get; set; }
    public Guid SubscriberId { get; set; }
    public string Email { get; set; } = string.Empty;
    public DateTimeOffset SubscribedAtUtc { get; set; }
}
