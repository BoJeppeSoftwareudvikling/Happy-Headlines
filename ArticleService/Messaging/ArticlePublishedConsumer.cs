using System.Diagnostics;
using System.Text.Json;
using ArticleService.Data;
using ArticleService.Models;
using Messaging;
using Messaging.Contracts;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Monitoring;
using OpenTelemetry;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ArticleService.Messaging;

public class ArticlePublishedConsumer(
    IOptions<RabbitMqOptions> options,
    ArticleDbContextFactory dbContextFactory,
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
            ClientProvidedName = $"ArticleService-{Environment.MachineName}",
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
            activity?.SetTag("messaging.destination.name", ArticleQueueTopology.ArticleServiceQueue);
            activity?.SetTag("messaging.operation.name", "process");
            activity?.SetTag("messaging.message.id", eventArgs.BasicProperties.MessageId);

            try
            {
                var message = JsonSerializer.Deserialize<ArticlePublished>(eventArgs.Body.Span)
                    ?? throw new JsonException("Article message was empty.");

                if (message.MessageId == Guid.Empty)
                {
                    throw new JsonException("Article message has no message ID.");
                }

                if (!Enum.TryParse<Continent>(message.Continent, out var continent) ||
                    !Enum.IsDefined(continent))
                {
                    throw new JsonException($"Unsupported continent '{message.Continent}'.");
                }

                await using var db = dbContextFactory.Create(continent);
                if (await db.Articles.AnyAsync(
                        article => article.SourceMessageId == message.MessageId,
                        stoppingToken))
                {
                    logger.LogInformation(
                        "Article message {MessageId} was already stored; acknowledging redelivery.",
                        message.MessageId);
                    await channel.BasicAckAsync(
                        eventArgs.DeliveryTag,
                        multiple: false,
                        cancellationToken: stoppingToken);
                    return;
                }

                var article = new Article
                {
                    Title = message.Title,
                    Content = message.Content,
                    Continent = continent,
                    SourceMessageId = message.MessageId
                };
                db.Articles.Add(article);
                await db.SaveChangesAsync(stoppingToken);

                logger.LogInformation(
                    "Stored published article {MessageId} as article {ArticleId} in {Continent}.",
                    message.MessageId,
                    article.Id,
                    continent);

                await channel.BasicAckAsync(
                    eventArgs.DeliveryTag,
                    multiple: false,
                    cancellationToken: stoppingToken);
            }
            catch (DbUpdateException ex) when (IsDuplicateMessage(ex))
            {
                logger.LogInformation(
                    "Article message {MessageId} was stored by another consumer; acknowledging redelivery.",
                    eventArgs.BasicProperties.MessageId);
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
                logger.LogError(ex, "Could not persist article message; requeueing it.");
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
            ArticleQueueTopology.ArticleServiceQueue,
            autoAck: false,
            consumer,
            cancellationToken: stoppingToken);

        logger.LogInformation(
            "Listening for published articles on {QueueName}.",
            ArticleQueueTopology.ArticleServiceQueue);

        await consumerStopped.Task.WaitAsync(stoppingToken);
        throw new InvalidOperationException("RabbitMQ article consumer stopped unexpectedly.");
    }

    private static bool IsDuplicateMessage(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };
}
