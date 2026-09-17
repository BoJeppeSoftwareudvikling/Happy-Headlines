using DraftService.Data;
using DraftService.Infrastructure;
using Monitoring;

_ = MonitorService.TracerProvider;

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    builder.Services.AddSingleton<DraftDbContextFactory>();
    builder.Services.AddSingleton<DraftDatabaseInitializer>();
    builder.Services.Configure<DatabaseInitializationOptions>(
        builder.Configuration.GetSection(DatabaseInitializationOptions.SectionName));

    var app = builder.Build();

    var initializer = app.Services.GetRequiredService<DraftDatabaseInitializer>();
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
