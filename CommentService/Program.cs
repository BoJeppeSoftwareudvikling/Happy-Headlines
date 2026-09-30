using CommentService.Clients;
using CommentService.Data;
using CommentService.Infrastructure;
using CommentService.Options;
using Microsoft.Extensions.Options;
using Monitoring;
using Polly;
using CommentService.Caching;
using Prometheus;

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
        builder.Configuration.GetSection(DatabaseInitializationOptions.SectionName));
    builder.Services.AddCommentCacheInfrastructure(builder.Configuration);
    builder.Services.Configure<ProfanityServiceOptions>(
        builder.Configuration.GetSection(ProfanityServiceOptions.SectionName));
    builder.Services.AddSingleton<IAsyncPolicy<HttpResponseMessage>>(serviceProvider =>
    {
        var options = serviceProvider.GetRequiredService<IOptions<ProfanityServiceOptions>>().Value;
        var logger = serviceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("ProfanityServiceCircuitBreaker");

        return Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: options.FailureThreshold,
                durationOfBreak: TimeSpan.FromSeconds(options.BreakDurationSeconds),
                onBreak: (outcome, duration) =>
                {
                    logger.LogWarning(
                        "ProfanityService circuit opened for {DurationSeconds}s.",
                        duration.TotalSeconds);
                },
                onReset: () =>
                {
                    logger.LogInformation("ProfanityService circuit closed.");
                });
    });
    builder.Services.AddHttpClient<ProfanityClient>((serviceProvider, client) =>
    {
        var options = serviceProvider.GetRequiredService<IOptions<ProfanityServiceOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    })
    .AddPolicyHandler((serviceProvider, _) =>
        serviceProvider.GetRequiredService<IAsyncPolicy<HttpResponseMessage>>());

    var app = builder.Build();

    var initializer = app.Services.GetRequiredService<CommentDatabaseInitializer>();
    await initializer.InitializeAsync(app.Lifetime.ApplicationStopping);

    _ = app.Services.GetRequiredService<StackExchange.Redis.IConnectionMultiplexer>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseRouting();
    app.UseHttpMetrics(options =>
    {
        options.ReduceStatusCodeCardinality();
        options.CaptureMetricsUrl = false;
    });
    app.UseAuthorization();

    app.MapControllers();
    app.MapMetrics();

    await app.RunAsync();
}
finally
{
    MonitorService.Shutdown();
}
