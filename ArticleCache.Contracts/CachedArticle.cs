namespace ArticleCache.Contracts;

public sealed record CachedArticle
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string Continent { get; init; } = string.Empty;
    public DateTimeOffset PublishedAtUtc { get; init; }
}