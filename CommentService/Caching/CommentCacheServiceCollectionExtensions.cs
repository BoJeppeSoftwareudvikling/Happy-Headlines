using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace CommentService.Caching;

public static class CommentCacheServiceCollectionExtensions
{
    public static IServiceCollection AddCommentCacheInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<CommentCacheOptions>()
            .Bind(configuration.GetSection(CommentCacheOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                "CommentCache:ConnectionString is required.")
            .Validate(
                options => options.EntryTtlSeconds > 0,
                "CommentCache:EntryTtlSeconds must be positive.")
            .Validate(
                options => options.MaximumArticles > 0,
                "CommentCache:MaximumArticles must be positive.")
            .ValidateOnStart();

        services.AddSingleton<IConnectionMultiplexer>(serviceProvider =>
        {
            var options = serviceProvider
                .GetRequiredService<IOptions<CommentCacheOptions>>().Value;

            var redisConfiguration =
                ConfigurationOptions.Parse(options.ConnectionString);

            redisConfiguration.AbortOnConnectFail = false;
            redisConfiguration.ConnectTimeout = 5000;
            redisConfiguration.AsyncTimeout = 5000;
            redisConfiguration.SyncTimeout = 5000;

            var connection = ConnectionMultiplexer.Connect(redisConfiguration);
            var logger = serviceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("RedisCommentCache");

            connection.ConnectionFailed += (_, args) =>
                logger.LogWarning(
                    args.Exception,
                    "RedisCommentCache connection failed at {Endpoint}: {FailureType}.",
                    args.EndPoint,
                    args.FailureType);

            connection.ConnectionRestored += (_, args) =>
                logger.LogInformation(
                    "RedisCommentCache connection restored at {Endpoint}.",
                    args.EndPoint);

            if (!connection.IsConnected)
            {
                logger.LogWarning("RedisCommentCache was unavailable at startup.");
            }

            return connection;
        });

        services.AddSingleton<ICommentCache, RedisCommentCache>();

        return services;
    }
}