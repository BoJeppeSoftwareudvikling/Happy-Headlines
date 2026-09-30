namespace CommentService.Caching;

public static class CommentCacheScripts
{
    public const string HitAndTouch =
        """
        local value = redis.call('GET', KEYS[1])
        if not value then
            redis.call('ZREM', KEYS[2], ARGV[1])
            return nil
        end

        local now = redis.call('TIME')
        local score = tonumber(now[1]) * 1000000 + tonumber(now[2])
        redis.call('ZADD', KEYS[2], score, ARGV[1])
        return value
        """;

    public const string StoreAndTrim =
        """
        redis.call('SET', KEYS[2], ARGV[2], 'EX', ARGV[3])

        local now = redis.call('TIME')
        local score = tonumber(now[1]) * 1000000 + tonumber(now[2])
        redis.call('ZADD', KEYS[1], score, ARGV[1])

        local members = redis.call('ZRANGE', KEYS[1], 0, -1)
        for _, member in ipairs(members) do
            if redis.call('EXISTS', ARGV[5] .. member) == 0 then
                redis.call('ZREM', KEYS[1], member)
            end
        end

        local evicted = {}
        local capacity = tonumber(ARGV[4])

        while redis.call('ZCARD', KEYS[1]) > capacity do
            local oldest = redis.call('ZRANGE', KEYS[1], 0, 0)
            if #oldest == 0 then
                break
            end

            local articleId = oldest[1]
            redis.call('ZREM', KEYS[1], articleId)
            redis.call('DEL', ARGV[5] .. articleId)
            table.insert(evicted, articleId)
        end

        return evicted
        """;

    public const string Invalidate =
        """
        local deleted = redis.call('DEL', KEYS[1])
        redis.call('ZREM', KEYS[2], ARGV[1])
        return deleted
        """;
}
