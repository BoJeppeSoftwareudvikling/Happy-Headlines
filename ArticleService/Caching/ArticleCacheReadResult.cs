namespace ArticleService.Caching;

public enum ArticleCacheReadOutcome
{
    Hit,
    Miss,
    Error
}

public sealed record ArticleCacheReadResult<T>(
    ArticleCacheReadOutcome Outcome,
    T? Value)
{
    public static ArticleCacheReadResult<T> Hit(T value) =>
        new(ArticleCacheReadOutcome.Hit, value);

    public static ArticleCacheReadResult<T> Miss() =>
        new(ArticleCacheReadOutcome.Miss, default);

    public static ArticleCacheReadResult<T> Error() =>
        new(ArticleCacheReadOutcome.Error, default);
}