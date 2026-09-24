using Messaging;
using Monitoring;
using PublisherService.Clients;
using PublisherService.Messaging;
using PublisherService.Options;

_ = MonitorService.TracerProvider;

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    builder.Services.Configure<RabbitMqOptions>(
        builder.Configuration.GetSection(RabbitMqOptions.SectionName));
    builder.Services.Configure<ProfanityServiceOptions>(
        builder.Configuration.GetSection(ProfanityServiceOptions.SectionName));
    builder.Services.AddSingleton<ArticlePublisher>();
    builder.Services.AddHttpClient<ProfanityClient>((serviceProvider, client) =>
    {
        var options = serviceProvider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<ProfanityServiceOptions>>()
            .Value;
        client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    });

    var app = builder.Build();

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
