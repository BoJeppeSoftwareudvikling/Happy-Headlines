namespace ArticleCache.Contracts;

public static class ArticleCacheKeys
{
    private const string Prefix = "happy-headlines:article-cache";

    public const string ActiveVersion = $"{Prefix}:active-version";

    public static string SnapshotItems(string version) =>
        $"{Prefix}:snapshot:{version}:items";

    public static string SnapshotPublished(string version) =>
        $"{Prefix}:snapshot:{version}:published";
}