using System.Text.Json.Serialization;

namespace ArticleService.Models;

public enum Continent
{
    Africa,
    Asia,
    Europe,
    NorthAmerica,
    SouthAmerica,
    Oceania,
    Antarctica,
    Global,
}
public class Article
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public Continent Continent { get; set; }
    public DateTimeOffset PublishedAtUtc { get; set; }

    [JsonIgnore]
    public Guid? SourceMessageId { get; set; }
}
