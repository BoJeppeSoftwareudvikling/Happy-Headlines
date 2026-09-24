using Messaging.Contracts;

namespace NewsletterService.Services;

public class NewsletterFeed
{
    private const int Capacity = 20;
    private readonly object _sync = new();
    private readonly List<ArticlePublished> _articles = [];
    private readonly HashSet<Guid> _seenMessageIds = [];

    public void Add(ArticlePublished article)
    {
        lock (_sync)
        {
            if (!_seenMessageIds.Add(article.MessageId))
            {
                return;
            }

            _articles.Insert(0, article);
            if (_articles.Count > Capacity)
            {
                _articles.RemoveRange(Capacity, _articles.Count - Capacity);
            }
        }
    }

    public IReadOnlyList<ArticlePublished> GetLatest()
    {
        lock (_sync)
        {
            return _articles.ToList();
        }
    }
}
