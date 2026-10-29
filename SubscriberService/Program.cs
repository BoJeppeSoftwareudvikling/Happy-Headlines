using Messaging;
using Monitoring;
using SubscriberService.Data;
using SubscriberService.Features;
using SubscriberService.Infrastructure;
using SubscriberService.Messaging;

_ = MonitorService.TracerProvider;

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddControllers(options =>
        options.Filters.Add<SubscriberReleaseToggleFilter>());
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.Services.AddSingleton<SubscriberDbContextFactory>();
    builder.Services.AddSingleton<SubscriberDatabaseInitializer>();
    builder.Services.Configure<DatabaseInitializationOptions>(
        builder.Configuration.GetSection(DatabaseInitializationOptions.SectionName));

    builder.Services.Configure<SubscriberQueueOptions>(
        builder.Configuration.GetSection(SubscriberQueueOptions.SectionName));
    builder.Services.AddSingleton<SubscriberPublisher>();
    builder.Services.AddHostedService<SubscriberOutboxPublisher>();

    builder.Services.Configure<FeatureHubOptions>(
        builder.Configuration.GetSection(FeatureHubOptions.SectionName));
    builder.Services.AddSingleton<FeatureHubSubscriberReleaseToggle>();
    builder.Services.AddSingleton<ISubscriberReleaseToggle>(serviceProvider =>
        serviceProvider.GetRequiredService<FeatureHubSubscriberReleaseToggle>());
    builder.Services.AddHostedService(serviceProvider =>
        serviceProvider.GetRequiredService<FeatureHubSubscriberReleaseToggle>());

    var app = builder.Build();

    var initializer = app.Services.GetRequiredService<SubscriberDatabaseInitializer>();
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
