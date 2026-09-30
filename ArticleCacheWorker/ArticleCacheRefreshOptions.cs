namespace ArticleCacheWorker;

public sealed class ArticleCacheOptions
{
    public const string SectionName = "ArticleCache";

    public string ConnectionString { get; set; } = string.Empty;
    public int SnapshotTtlSeconds { get; set; } = 900;
}

public sealed class ArticleCacheRefreshOptions
{
    public const string SectionName = "ArticleCacheRefresh";

    public int IntervalSeconds { get; set; } = 300;
    public int RetryDelaySeconds { get; set; } = 10;
}

public sealed class GlobalArticleDatabaseOptions
{
    public const string SectionName = "ConnectionStrings";

    public string Global { get; set; } = string.Empty;
}