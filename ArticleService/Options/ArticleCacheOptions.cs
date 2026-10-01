namespace ArticleService.Options;

public sealed class ArticleCacheOptions
{
    public const string SectionName = "ArticleCache";

    public string ConnectionString { get; init; } = string.Empty;
    public int WindowDays { get; init; } = 14;
    public int RefreshIntervalSeconds { get; init; } = 300;
}
