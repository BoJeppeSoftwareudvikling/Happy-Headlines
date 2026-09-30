using CommentService.Models;

namespace CommentService.Caching;

public interface ICommentCache
{
    Task<CommentCacheReadResult<IReadOnlyList<Comment>>> GetAsync(
        int articleId,
        CancellationToken cancellationToken);

    Task StoreAsync(
        int articleId,
        IReadOnlyList<Comment> comments,
        CancellationToken cancellationToken);

    Task InvalidateAsync(
        int articleId,
        CancellationToken cancellationToken);
}
