namespace CommentService.Caching;

public sealed class CommentCacheOptions
{
    public const string SectionName = "CommentCache";

    public string ConnectionString { get; set; } = string.Empty;
    public int EntryTtlSeconds { get; set; } = 900;
    public int MaximumArticles { get; set; } = 30;
}