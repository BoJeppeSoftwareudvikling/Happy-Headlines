using System.Net;
using System.Text.Json.Serialization;
using CommentService.Clients;
using CommentService.Data;
using CommentService.Infrastructure;
using CommentService.Options;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<CommentDbContextFactory>();
builder.Services.Configure<DatabaseInitializationOptions>(
    builder.Configuration.GetSection(DatabaseInitializationOptions.SectionName));
builder.Services.AddSingleton<CommentDatabaseInitializer>();

builder.Services.Configure<ProfanityServiceOptions>(
    builder.Configuration.GetSection(ProfanityServiceOptions.SectionName));

builder.Services.AddSingleton<AsyncCircuitBreakerPolicy<HttpResponseMessage>>(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<ProfanityServiceOptions>>().Value;
    var logger = serviceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("ProfanityServiceCircuitBreaker");

    return Policy<HttpResponseMessage>
        .Handle<HttpRequestException>()
        .Or<TaskCanceledException>()
        .OrResult(response =>
            response.StatusCode == HttpStatusCode.RequestTimeout ||
            (int)response.StatusCode >= 500)
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
            },
            onHalfOpen: () =>
            {
                logger.LogInformation("ProfanityService circuit is half-open.");
            });
});

builder.Services.AddHttpClient<IProfanityClient, ProfanityClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<ProfanityServiceOptions>>().Value;

    client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
})
.AddPolicyHandler((serviceProvider, _) =>
    serviceProvider.GetRequiredService<AsyncCircuitBreakerPolicy<HttpResponseMessage>>());

var app = builder.Build();

var initializer = app.Services.GetRequiredService<CommentDatabaseInitializer>();
await initializer.InitializeAsync(app.Lifetime.ApplicationStopping);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

await app.RunAsync();
