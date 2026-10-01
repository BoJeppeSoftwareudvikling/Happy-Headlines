using CommentService.Caching;
using CommentService.Clients;
using CommentService.Data;
using CommentService.Infrastructure;
using CommentService.Options;
using Microsoft.Extensions.Options;
using Monitoring;
using Polly;
using StackExchange.Redis;

_ = MonitorService.TracerProvider;

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.Services.AddSingleton<CommentDbContextFactory>();
    builder.Services.AddSingleton<CommentDatabaseInitializer>();
    builder.Services.Configure<DatabaseInitializationOptions>(
        builder.Configuration.GetSection(
            DatabaseInitializationOptions.SectionName));

    builder.Services.Configure<ProfanityServiceOptions>(
        builder.Configuration.GetSection(
            ProfanityServiceOptions.SectionName));

    builder.Services.AddSingleton<IAsyncPolicy<HttpResponseMessage>>(
        serviceProvider =>
        {
            var options = serviceProvider
                .GetRequiredService<
                    IOptions<ProfanityServiceOptions>>()
                .Value;

            var logger = serviceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger(
                    "ProfanityServiceCircuitBreaker");

            return Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .Or<TaskCanceledException>()
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking:
                        options.FailureThreshold,
                    durationOfBreak: TimeSpan.FromSeconds(
                        options.BreakDurationSeconds),
                    onBreak: (_, duration) =>
                    {
                        logger.LogWarning(
                            "ProfanityService circuit opened for {DurationSeconds}s.",
                            duration.TotalSeconds);
                    },
                    onReset: () =>
                    {
                        logger.LogInformation(
                            "ProfanityService circuit closed.");
                    });
        });

    builder.Services.AddHttpClient<ProfanityClient>(
        (serviceProvider, client) =>
        {
            var options = serviceProvider
                .GetRequiredService<
                    IOptions<ProfanityServiceOptions>>()
                .Value;

            client.BaseAddress = new Uri(
                options.BaseUrl,
                UriKind.Absolute);

            client.Timeout = TimeSpan.FromSeconds(
                options.TimeoutSeconds);
        })
        .AddPolicyHandler((serviceProvider, _) =>
            serviceProvider.GetRequiredService<
                IAsyncPolicy<HttpResponseMessage>>());

    builder.Services
        .AddOptions<CommentCacheOptions>()
        .Bind(builder.Configuration.GetSection(
            CommentCacheOptions.SectionName))
        .Validate(
            options => !string.IsNullOrWhiteSpace(
                options.ConnectionString),
            "CommentCache:ConnectionString is required.")
        .ValidateOnStart();

    builder.Services.AddSingleton<IConnectionMultiplexer>(
        serviceProvider =>
        {
            var options = serviceProvider
                .GetRequiredService<
                    IOptions<CommentCacheOptions>>()
                .Value;

            var redisOptions = ConfigurationOptions.Parse(
                options.ConnectionString);

            redisOptions.AbortOnConnectFail = false;

            return ConnectionMultiplexer.Connect(redisOptions);
        });

    builder.Services.AddSingleton<ICommentCache, RedisCommentCache>();
    builder.Services.AddSingleton<CacheOperationMetrics>();
    builder.Services.AddSingleton<HttpRequestMetrics>();

    var app = builder.Build();

    var initializer =
        app.Services.GetRequiredService<CommentDatabaseInitializer>();

    await initializer.InitializeAsync(
        app.Lifetime.ApplicationStopping);

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHappyHeadlinesRequestMetrics();
    app.UseAuthorization();

    app.MapControllers();

    app.MapGet(
        "/metrics/cache",
        async Task<IResult> (
            ICommentCache cache,
            CacheOperationMetrics operationMetrics,
            HttpRequestMetrics requestMetrics,
            ILoggerFactory loggerFactory) =>
        {
            CacheMetricsSnapshot? snapshot = null;

            try
            {
                snapshot = await cache
                    .GetMetricsAsync()
                    .WaitAsync(TimeSpan.FromSeconds(1));
            }
            catch (Exception ex)
                when (ex is RedisException or TimeoutException)
            {
                loggerFactory
                    .CreateLogger("CommentCacheMetrics")
                    .LogWarning(
                        ex,
                        "CommentCache metrics are unavailable or timed out.");

            }

            return Results.Text(
                PrometheusCacheMetricsFormatter.Format(
                    "comment",
                    "CommentService",
                    snapshot,
                    operationMetrics.GetSnapshot(),
                    requestMetrics.GetSnapshot()),
                "text/plain; version=0.0.4; charset=utf-8");
        });

    await app.RunAsync();
}
finally
{
    MonitorService.Shutdown();
}
