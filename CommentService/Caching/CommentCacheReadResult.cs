namespace CommentService.Caching;

public enum CommentCacheReadOutcome
{
    Hit,
    Miss,
    Error
}

public sealed record CommentCacheReadResult<T>(
    CommentCacheReadOutcome Outcome,
    T? Value)
{
    public static CommentCacheReadResult<T> Hit(T value) =>
        new(CommentCacheReadOutcome.Hit, value);

    public static CommentCacheReadResult<T> Miss() =>
        new(CommentCacheReadOutcome.Miss, default);

    public static CommentCacheReadResult<T> Error() =>
        new(CommentCacheReadOutcome.Error, default);
}
