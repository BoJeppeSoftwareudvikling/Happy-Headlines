using ArticleService.Data;
using ArticleService.Infrastructure;
using ArticleService.Messaging;
using Messaging;
using Monitoring;
using ArticleService.Caching;
using Prometheus;

_ = MonitorService.TracerProvider;

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    builder.Services.AddSingleton<ArticleDbContextFactory>();
    builder.Services.Configure<DatabaseInitializationOptions>(
        builder.Configuration.GetSection(DatabaseInitializationOptions.SectionName));
    builder.Services.AddSingleton<ArticleDatabaseInitializer>();
    builder.Services.Configure<RabbitMqOptions>(
        builder.Configuration.GetSection(RabbitMqOptions.SectionName));
    builder.Services.AddHostedService<ArticlePublishedConsumer>();
    builder.Services.AddArticleCacheInfrastructure(builder.Configuration);

    var app = builder.Build();

    var initializer = app.Services.GetRequiredService<ArticleDatabaseInitializer>();
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
