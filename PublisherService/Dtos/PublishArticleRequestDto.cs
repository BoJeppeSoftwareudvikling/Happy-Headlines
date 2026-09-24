namespace PublisherService.Dtos;

public class PublishArticleRequestDto
{
    public Guid MessageId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Continent { get; set; } = "Global";
}
