using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ArticleService.Caching;

public static class ArticleCacheServiceCollectionExtensions
{
    public static IServiceCollection AddArticleCacheInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<ArticleCacheOptions>()
            .Bind(configuration.GetSection(ArticleCacheOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                "ArticleCache:ConnectionString is required.")
            .Validate(
                options => options.DefaultRecentLimit > 0,
                "ArticleCache:DefaultRecentLimit must be positive.")
            .Validate(
                options => options.MaximumRecentLimit >= options.DefaultRecentLimit,
                "MaximumRecentLimit must be at least DefaultRecentLimit.")
            .ValidateOnStart();

        services.AddSingleton<IConnectionMultiplexer>(serviceProvider =>
        {
            var options = serviceProvider
                .GetRequiredService<IOptions<ArticleCacheOptions>>().Value;

            var redisConfiguration =
                ConfigurationOptions.Parse(options.ConnectionString);

            redisConfiguration.AbortOnConnectFail = false;
            redisConfiguration.ConnectTimeout = 5000;
            redisConfiguration.AsyncTimeout = 5000;
            redisConfiguration.SyncTimeout = 5000;

            var connection = ConnectionMultiplexer.Connect(redisConfiguration);
            var logger = serviceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("RedisArticleCache");

            connection.ConnectionFailed += (_, args) =>
                logger.LogWarning(
                    args.Exception,
                    "RedisArticleCache connection failed at {Endpoint}: {FailureType}.",
                    args.EndPoint,
                    args.FailureType);

            connection.ConnectionRestored += (_, args) =>
                logger.LogInformation(
                    "RedisArticleCache connection restored at {Endpoint}.",
                    args.EndPoint);

            if (!connection.IsConnected)
            {
                logger.LogWarning("RedisArticleCache was unavailable at startup.");
            }

            return connection;
        });

        services.AddSingleton<IArticleCache, RedisArticleCache>();

        return services;
    }
}