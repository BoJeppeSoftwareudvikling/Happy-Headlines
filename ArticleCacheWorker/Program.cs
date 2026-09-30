using ArticleCacheWorker;
using Microsoft.Extensions.Options;
using Monitoring;
using StackExchange.Redis;

_ = MonitorService.TracerProvider;

try
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddOptions<GlobalArticleDatabaseOptions>()
        .Bind(builder.Configuration.GetSection(
            GlobalArticleDatabaseOptions.SectionName))
        .Validate(
            options => !string.IsNullOrWhiteSpace(options.Global),
            "ConnectionStrings:Global is required.")
        .ValidateOnStart();

    builder.Services.AddOptions<ArticleCacheOptions>()
        .Bind(builder.Configuration.GetSection(
            ArticleCacheOptions.SectionName))
        .Validate(
            options => !string.IsNullOrWhiteSpace(options.ConnectionString),
            "ArticleCache:ConnectionString is required.")
        .Validate(
            options => options.SnapshotTtlSeconds > 0,
            "ArticleCache:SnapshotTtlSeconds must be positive.")
        .ValidateOnStart();

    builder.Services.AddOptions<ArticleCacheRefreshOptions>()
        .Bind(builder.Configuration.GetSection(
            ArticleCacheRefreshOptions.SectionName))
        .Validate(
            options => options.IntervalSeconds > 0,
            "ArticleCacheRefresh:IntervalSeconds must be positive.")
        .Validate(
            options => options.RetryDelaySeconds > 0,
            "ArticleCacheRefresh:RetryDelaySeconds must be positive.")
        .ValidateOnStart();

    builder.Services.AddSingleton<IConnectionMultiplexer>(serviceProvider =>
    {
        var options = serviceProvider
            .GetRequiredService<IOptions<ArticleCacheOptions>>().Value;

        var configuration =
            ConfigurationOptions.Parse(options.ConnectionString);

        configuration.AbortOnConnectFail = false;
        configuration.ConnectTimeout = 5000;
        configuration.AsyncTimeout = 5000;
        configuration.SyncTimeout = 5000;

        var connection = ConnectionMultiplexer.Connect(configuration);
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

        return connection;
    });

    builder.Services.AddSingleton<ArticleSnapshotWriter>();
    builder.Services.AddHostedService<Worker>();

    using var host = builder.Build();

    _ = host.Services.GetRequiredService<IConnectionMultiplexer>();

    await host.RunAsync();
}
finally
{
    MonitorService.Shutdown();
}