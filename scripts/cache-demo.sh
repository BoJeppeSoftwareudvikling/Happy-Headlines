#!/usr/bin/env bash

set -euo pipefail

article_url="${ARTICLE_URL:-http://127.0.0.1:8080}"
comment_url="${COMMENT_URL:-http://127.0.0.1:5049}"
article_id="${1:-}"

request() {
  curl --fail --silent --show-error "$1" >/dev/null
}

echo "ArticleCache: request recent articles twice"
request "${article_url}/Article/recent?continent=Global&limit=10"
request "${article_url}/Article/recent?continent=Global&limit=10"

if [[ -n "${article_id}" ]]; then
  echo "ArticleCache: request article ${article_id}"
  request "${article_url}/Article/${article_id}?continent=Global"
fi

echo "CommentCache: fill article entries 1 through 30"
for id in $(seq 1 30); do
  request "${comment_url}/Comment/article/${id}"
done

echo "CommentCache: touch article 1, then insert article 31"
request "${comment_url}/Comment/article/1"
request "${comment_url}/Comment/article/31"

redis_container="$(
  docker ps \
    --filter name=happyHeadlines_comment-cache \
    --format '{{.ID}}' |
    head -n 1
)"

if [[ -z "${redis_container}" ]]; then
  echo "Could not find the comment-cache container." >&2
  exit 1
fi

echo "Current LRU order, oldest first:"
docker exec "${redis_container}" \
  redis-cli ZRANGE happy-headlines:comment-cache:lru 0 -1

if docker exec "${redis_container}" \
  redis-cli ZSCORE happy-headlines:comment-cache:lru 2 |
  grep -q .; then
  echo "Expected article 2 to be evicted, but it remains in the LRU." >&2
  exit 1
fi

echo "LRU verification passed. Article 2 was evicted after article 1 was retouched."
echo "Article metrics: ${article_url}/metrics"
echo "Comment metrics: ${comment_url}/metrics"
