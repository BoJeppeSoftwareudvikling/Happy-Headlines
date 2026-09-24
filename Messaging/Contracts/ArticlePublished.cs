namespace Messaging.Contracts;

public class ArticlePublished
{
    public Guid MessageId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Continent { get; set; } = string.Empty;
    public DateTimeOffset PublishedAtUtc { get; set; }
}
