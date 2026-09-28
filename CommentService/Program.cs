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

    app.UseAuthorization();

    app.MapControllers();

    app.MapGet(
        "/metrics/cache",
        async Task<IResult> (
            ICommentCache cache,
            ILoggerFactory loggerFactory) =>
        {
            try
            {
                var snapshot = await cache.GetMetricsAsync();

                return Results.Text(
                    PrometheusCacheMetricsFormatter.Format(
                        "comment",
                        snapshot),
                    "text/plain; version=0.0.4; charset=utf-8");
            }
            catch (RedisException ex)
            {
                loggerFactory
                    .CreateLogger("CommentCacheMetrics")
                    .LogWarning(
                        ex,
                        "CommentCache metrics are unavailable.");

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
