using Messaging;
using Monitoring;
using NewsletterService.Messaging;
using NewsletterService.Services;

_ = MonitorService.TracerProvider;

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    builder.Services.Configure<RabbitMqOptions>(
        builder.Configuration.GetSection(RabbitMqOptions.SectionName));
    builder.Services.AddSingleton<NewsletterFeed>();
    builder.Services.AddHostedService<ArticlePublishedConsumer>();

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
