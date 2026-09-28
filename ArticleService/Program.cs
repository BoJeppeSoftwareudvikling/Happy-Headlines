using ArticleService.Caching;
using ArticleService.Data;
using ArticleService.Infrastructure;
using ArticleService.Messaging;
using ArticleService.Options;
using Messaging;
using Microsoft.Extensions.Options;
using Monitoring;
using StackExchange.Redis;

_ = MonitorService.TracerProvider;

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.Services.AddSingleton<ArticleDbContextFactory>();
    builder.Services.Configure<DatabaseInitializationOptions>(
        builder.Configuration.GetSection(
            DatabaseInitializationOptions.SectionName));
    builder.Services.AddSingleton<ArticleDatabaseInitializer>();

    builder.Services.Configure<RabbitMqOptions>(
        builder.Configuration.GetSection(RabbitMqOptions.SectionName));
    builder.Services.AddHostedService<ArticlePublishedConsumer>();

    builder.Services
        .AddOptions<ArticleCacheOptions>()
        .Bind(builder.Configuration.GetSection(
            ArticleCacheOptions.SectionName))
        .Validate(
            options => !string.IsNullOrWhiteSpace(
                options.ConnectionString),
            "ArticleCache:ConnectionString is required.")
        .Validate(
            options => options.WindowDays > 0,
            "ArticleCache:WindowDays must be greater than zero.")
        .Validate(
            options => options.RefreshIntervalSeconds > 0,
            "ArticleCache:RefreshIntervalSeconds must be greater than zero.")
        .ValidateOnStart();

    builder.Services.AddSingleton<IConnectionMultiplexer>(
        serviceProvider =>
        {
            var options = serviceProvider
                .GetRequiredService<IOptions<ArticleCacheOptions>>()
                .Value;

            var redisOptions = ConfigurationOptions.Parse(
                options.ConnectionString);

            redisOptions.AbortOnConnectFail = false;

            return ConnectionMultiplexer.Connect(redisOptions);
        });

    builder.Services.AddSingleton<IArticleCache, RedisArticleCache>();
    builder.Services.AddHostedService<ArticleCacheRefreshWorker>();

    var app = builder.Build();

    var initializer =
        app.Services.GetRequiredService<ArticleDatabaseInitializer>();

    await initializer.InitializeAsync(
        app.Lifetime.ApplicationStopping);

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseAuthorization();

    app.MapControllers();

    app.MapGet(
        "/metrics/cache",
        async Task<IResult> (
            IArticleCache cache,
            ILoggerFactory loggerFactory) =>
        {
            try
            {
                var snapshot = await cache.GetMetricsAsync();

                return Results.Text(
                    PrometheusCacheMetricsFormatter.Format(
                        "article",
                        snapshot),
                    "text/plain; version=0.0.4; charset=utf-8");
            }
            catch (RedisException ex)
            {
                loggerFactory
                    .CreateLogger("ArticleCacheMetrics")
                    .LogWarning(
                        ex,
                        "ArticleCache metrics are unavailable.");

                return Results.StatusCode(
                    StatusCodes.Status503ServiceUnavailable);
            }
        });

    await app.RunAsync();
}
finally
{
    MonitorService.Shutdown();
}
