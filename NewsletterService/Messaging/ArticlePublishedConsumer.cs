using System.Diagnostics;
using System.Text.Json;
using Messaging;
using Messaging.Contracts;
using Microsoft.Extensions.Options;
using Monitoring;
using NewsletterService.Services;
using OpenTelemetry;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NewsletterService.Messaging;

public class ArticlePublishedConsumer(
    IOptions<RabbitMqOptions> options,
    NewsletterFeed feed,
    ILogger<ArticlePublishedConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "RabbitMQ consumer stopped. Retrying in 5 seconds.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        var rabbitMq = options.Value;
        var factory = new ConnectionFactory
        {
            HostName = rabbitMq.HostName,
            Port = rabbitMq.Port,
            UserName = rabbitMq.UserName,
            Password = rabbitMq.Password,
            ClientProvidedName = $"NewsletterService-{Environment.MachineName}",
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true
        };

        await using var connection = await factory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        await ArticleQueueTopology.DeclareAsync(channel, stoppingToken);
        await channel.BasicQosAsync(0, 1, global: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        var consumerStopped = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        consumer.ShutdownAsync += (_, _) =>
        {
            consumerStopped.TrySetResult();
            return Task.CompletedTask;
        };
        consumer.UnregisteredAsync += (_, _) =>
        {
            consumerStopped.TrySetResult();
            return Task.CompletedTask;
        };
        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            var parentContext = RabbitMqTraceContext.Extract(eventArgs.BasicProperties.Headers);
            var previousBaggage = Baggage.Current;
            Baggage.Current = parentContext.Baggage;

            using var activity = MonitorService.ActivitySource.StartActivity(
                "articles process",
                ActivityKind.Consumer,
                parentContext.ActivityContext);
            activity?.SetTag("messaging.system", "rabbitmq");
            activity?.SetTag("messaging.destination.name", ArticleQueueTopology.NewsletterServiceQueue);
            activity?.SetTag("messaging.operation.name", "process");
            activity?.SetTag("messaging.message.id", eventArgs.BasicProperties.MessageId);

            try
            {
                var article = JsonSerializer.Deserialize<ArticlePublished>(eventArgs.Body.Span)
                    ?? throw new JsonException("Article message was empty.");

                feed.Add(article);
                logger.LogInformation(
                    "Prepared immediate newsletter content for article {MessageId}: {Title}",
                    article.MessageId,
                    article.Title);

                await channel.BasicAckAsync(
                    eventArgs.DeliveryTag,
                    multiple: false,
                    cancellationToken: stoppingToken);
            }
            catch (JsonException ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                logger.LogError(ex, "Discarding invalid article message.");
                await channel.BasicRejectAsync(
                    eventArgs.DeliveryTag,
                    requeue: false,
                    cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                logger.LogError(ex, "Could not process article message; requeueing it.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                await channel.BasicNackAsync(
                    eventArgs.DeliveryTag,
                    multiple: false,
                    requeue: true,
                    cancellationToken: stoppingToken);
            }
            finally
            {
                Baggage.Current = previousBaggage;
            }
        };

        await channel.BasicConsumeAsync(
            ArticleQueueTopology.NewsletterServiceQueue,
            autoAck: false,
            consumer,
            cancellationToken: stoppingToken);

        logger.LogInformation(
            "Listening for published articles on {QueueName}.",
            ArticleQueueTopology.NewsletterServiceQueue);

        await consumerStopped.Task.WaitAsync(stoppingToken);
        throw new InvalidOperationException("RabbitMQ newsletter consumer stopped unexpectedly.");
    }
}
