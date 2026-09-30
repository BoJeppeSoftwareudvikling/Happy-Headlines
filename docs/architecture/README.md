# Architecture

This directory describes the Happy Headlines containers that the cache assignment adds to the C4 model.

## Containers

```text
ArticleCache
[Container: Redis]
Offline cache med artikler fra de seneste 14 dage
```

```text
CommentCache
[Container: Redis]
Cache-aside med kommentarer til de 30 senest tilgåede artikler og LRU eviction
```

```text
ArticleService → ArticleCache
Finder artikler i cachen før databasen
```

```text
CommentService → CommentCache
Finder kommentarer i cachen før databasen
```

ArticleService owns the Docker service `article-cache`. CommentService owns `comment-cache`. Each cache has its own network. Redis is the technology inside the container. SQL Server remains the source of truth, shown in the architecture as `ArticleDatabase` and `CommentDatabase`.

## ArticleCache refresh

`ArticleCacheWorker` has one Swarm replica. It reads `ArticlesGlobal` for articles whose `PublishedAtUtc` is within the latest 14 days.

Each refresh writes a new Redis namespace:

```text
happy-headlines:article-cache:snapshot:{version}:items
happy-headlines:article-cache:snapshot:{version}:published
```

After the worker checks that the stored count matches the query, it updates:

```text
happy-headlines:article-cache:active-version
```

ArticleService reads only the active version. A failed refresh does not move that pointer. The snapshot TTL removes old and incomplete versions.

The worker runs every 300 seconds. The snapshot TTL is 900 seconds. Between refreshes, `ArticleCache` can be up to one interval behind `ArticleDatabase`, plus the retry delay if SQL Server or Redis is unavailable. If refresh stops, the snapshot expires and global reads fall back to SQL Server. A reader therefore never keeps a snapshot older than the TTL.

Global `GET /Article/{id}` and `GET /Article/recent` check `ArticleCache` first. A miss or a Redis error falls back to SQL Server and is counted separately. Regional reads skip `ArticleCache`. Updates and deletes remove the article from the active snapshot after the database commit.

Rows with no known `PublishedAtUtc` are stored as `1970-01-01T00:00:00Z`. That sentinel stays outside the 14-day window, so a schema migration does not make an undated article look newly published.

## CommentCache

CommentService stores one JSON array per article:

```text
happy-headlines:comment-cache:data:{articleId}
```

The array contains every comment for that article, in creation order. An empty array is a cached result.

Recency is a sorted set:

```text
happy-headlines:comment-cache:lru
```

Redis Lua scripts make read-and-touch, insert-and-evict, and invalidation atomic across both CommentService replicas. The insert script drops expired members and keeps at most 30 article entries. Each entry TTL is 900 seconds.

A comment is written to `CommentDatabase` first. CommentService then invalidates that article in `CommentCache`. A failed invalidation does not roll back the database write.

A concurrent GET can store an older list after that invalidation. The entry TTL limits how long the stale list remains. A version check or a distributed lock would close the window. The demonstration keeps the write-then-invalidate policy because the TTL bounds the race and the ordinary demo does not hit it.

A Redis miss and a Redis error both fall back to SQL Server. Metrics record them as different outcomes. Losing Redis does not delete articles or comments.

The worker rebuilds `ArticleCache` after Redis restarts. `CommentCache` fills again on the next miss.

## Monitoring

ArticleService and CommentService expose `/metrics`. Prometheus discovers Swarm tasks on the `monitoring` network. The `service` label is the Swarm service name. The `instance` label is that name plus the task slot, because Prometheus 3.5 does not provide `__meta_dockerswarm_task_name`.

Grafana reads Prometheus and provisions `Happy Headlines cache` from this repository. The dashboard shows separate hit ratios, hits, misses and errors for `ArticleCache` and `CommentCache`, plus `CommentCache` LRU evictions. Logs stay in Seq at `http://127.0.0.1:5342`. Traces stay in Zipkin at `http://127.0.0.1:9411`.

Cache counters:

```text
happy_headlines_cache_requests_total{cache="article|comment",outcome="hit|miss|error"}
happy_headlines_cache_fallbacks_total{cache,reason}
happy_headlines_cache_invalidations_total{cache,outcome}
happy_headlines_comment_cache_evictions_total
```

HTTP request count and duration are still exported at `/metrics`. Status codes are grouped as `2xx`, `4xx`, and `5xx`. The cache dashboard does not show them.

## Cache files

| File | Purpose |
|---|---|
| `ArticleCache.Contracts` | Snapshot contract and Redis key names shared by the worker and ArticleService |
| `ArticleCacheWorker` | One offline replica that publishes the 14-day snapshot |
| `ArticleService/Caching` | `ArticleCache` read, invalidation, miss/error result, and Redis connection |
| `CommentService/Caching` | `CommentCache` cache-aside, atomic LRU, and Redis connection |
| `Monitoring/CacheMetrics.cs` | Hit, miss, error, fallback, invalidation, and eviction counters |

## Container relationships

```text
ArticleCacheWorker -> ArticleDatabase
ArticleCacheWorker -> ArticleCache
ArticleService -> ArticleCache -> ArticleDatabase fallback
CommentService -> CommentCache -> CommentDatabase fallback
ArticleService /metrics -> Prometheus
CommentService /metrics -> Prometheus
Prometheus -> Grafana
```
