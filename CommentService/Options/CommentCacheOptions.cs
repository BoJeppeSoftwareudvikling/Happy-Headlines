namespace CommentService.Options;

public sealed class CommentCacheOptions
{
    public const string SectionName = "CommentCache";

    public string ConnectionString { get; init; } = string.Empty;
}
