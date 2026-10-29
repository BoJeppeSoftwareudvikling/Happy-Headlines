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
    builder.Services.Configure<SubscriberQueueOptions>(
        builder.Configuration.GetSection(SubscriberQueueOptions.SectionName));
    builder.Services.AddSingleton<NewsletterFeed>();
    builder.Services.AddSingleton<WelcomeEmailSender>();
    builder.Services.AddHostedService<ArticlePublishedConsumer>();
    builder.Services.AddHostedService<SubscriberRegisteredConsumer>();

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
