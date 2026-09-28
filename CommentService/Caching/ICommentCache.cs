using CommentService.Models;
using Monitoring;

namespace CommentService.Caching;

public interface ICommentCache
{
    Task<IReadOnlyList<Comment>?> GetAsync(int articleId);

    Task SetAsync(
        int articleId,
        IReadOnlyCollection<Comment> comments);

    Task RemoveAsync(int articleId);

    Task<CacheMetricsSnapshot> GetMetricsAsync();
}
