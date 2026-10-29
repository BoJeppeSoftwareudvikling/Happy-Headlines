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

public class SubscriberRegisteredConsumer(
    IOptions<SubscriberQueueOptions> options,
    WelcomeEmailSender emailSender,
    ILogger<SubscriberRegisteredConsumer> logger) : BackgroundService
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
                logger.LogError(ex, "Subscriber queue consumer stopped. Retrying in 5 seconds.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        var subscriberQueue = options.Value;
        var factory = new ConnectionFactory
        {
            HostName = subscriberQueue.HostName,
            Port = subscriberQueue.Port,
            UserName = subscriberQueue.UserName,
            Password = subscriberQueue.Password,
            ClientProvidedName = $"NewsletterService-Subscribers-{Environment.MachineName}",
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true
        };

        await using var connection = await factory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        await SubscriberQueueTopology.DeclareAsync(channel, stoppingToken);
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
                "subscriber registration process",
                ActivityKind.Consumer,
                parentContext.ActivityContext);
            activity?.SetTag("messaging.system", "rabbitmq");
            activity?.SetTag("messaging.destination.name", SubscriberQueueTopology.NewsletterServiceQueue);
            activity?.SetTag("messaging.operation.name", "process");
            activity?.SetTag("messaging.message.id", eventArgs.BasicProperties.MessageId);

            try
            {
                var subscriber = JsonSerializer.Deserialize<SubscriberRegistered>(eventArgs.Body.Span)
                    ?? throw new JsonException("Subscriber message was empty.");

                if (subscriber.MessageId == Guid.Empty
                    || subscriber.SubscriberId == Guid.Empty
                    || string.IsNullOrWhiteSpace(subscriber.Email))
                {
                    throw new JsonException("Subscriber message was missing required values.");
                }

                await emailSender.SendAsync(subscriber.Email, stoppingToken);
                await channel.BasicAckAsync(
                    eventArgs.DeliveryTag,
                    multiple: false,
                    cancellationToken: stoppingToken);
            }
            catch (JsonException ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                logger.LogError(ex, "Discarding invalid subscriber message.");
                await channel.BasicRejectAsync(
                    eventArgs.DeliveryTag,
                    requeue: false,
                    cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                logger.LogError(ex, "Could not handle subscriber message; requeueing it.");
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
            SubscriberQueueTopology.NewsletterServiceQueue,
            autoAck: false,
            consumer,
            cancellationToken: stoppingToken);

        logger.LogInformation(
            "Listening for new subscribers on {QueueName}.",
            SubscriberQueueTopology.NewsletterServiceQueue);

        await consumerStopped.Task.WaitAsync(stoppingToken);
        throw new InvalidOperationException("RabbitMQ subscriber consumer stopped unexpectedly.");
    }
}
