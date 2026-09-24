using ArticleService.Data;
using ArticleService.Infrastructure;
using ArticleService.Messaging;
using Messaging;
using Monitoring;

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

    var app = builder.Build();

    var initializer = app.Services.GetRequiredService<ArticleDatabaseInitializer>();
    await initializer.InitializeAsync(app.Lifetime.ApplicationStopping);

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseAuthorization();

    app.MapControllers();

    await app.RunAsync();
}
finally
{
    MonitorService.Shutdown();
}
