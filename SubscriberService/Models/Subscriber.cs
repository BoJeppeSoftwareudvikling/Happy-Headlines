namespace SubscriberService.Models;

public class Subscriber
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public DateTimeOffset SubscribedAtUtc { get; set; }
}
