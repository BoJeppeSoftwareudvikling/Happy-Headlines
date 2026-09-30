namespace ArticleService.Caching;

public sealed class ArticleCacheOptions
{
    public const string SectionName = "ArticleCache";

    public string ConnectionString { get; set; } = string.Empty;
    public int DefaultRecentLimit { get; set; } = 10;
    public int MaximumRecentLimit { get; set; } = 100;
}